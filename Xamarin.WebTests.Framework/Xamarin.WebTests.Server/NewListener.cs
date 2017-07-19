//
// NewListener.cs
//
// Author:
//       Martin Baulig <mabaul@microsoft.com>
//
// Copyright (c) 2017 Xamarin Inc. (http://www.xamarin.com)
//
// Permission is hereby granted, free of charge, to any person obtaining a copy
// of this software and associated documentation files (the "Software"), to deal
// in the Software without restriction, including without limitation the rights
// to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
// copies of the Software, and to permit persons to whom the Software is
// furnished to do so, subject to the following conditions:
//
// The above copyright notice and this permission notice shall be included in
// all copies or substantial portions of the Software.
//
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
// IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
// FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
// AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
// LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
// OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN
// THE SOFTWARE.
using System;
using System.IO;
using System.Net;
using System.Linq;
using System.Text;
using System.Threading;
using System.Reflection;
using System.Net.Sockets;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Net.Security;
using System.Security.Authentication;
using System.Security.Cryptography.X509Certificates;
using SD = System.Diagnostics;

using Xamarin.AsyncTests;
using Xamarin.AsyncTests.Portable;

namespace Xamarin.WebTests.Server
{
	using ConnectionFramework;
	using HttpFramework;
	using TestFramework;
	using HttpHandlers;

	abstract class NewListener : IDisposable
	{
		LinkedList<Context> connections;
		volatile bool disposed;
		volatile bool closed;

		int running;
		CancellationTokenSource cts;
		AsyncManualResetEvent mainLoopEvent;
		Dictionary<string, HttpOperation> registry;

		int requestParallelConnections;

		static int nextID;
		static long nextRequestID;
		public readonly int ID = ++nextID;

		internal object SyncRoot {
			get;
		}

		internal TestContext TestContext {
			get;
		}

		internal HttpServer Server {
			get;
		}

		internal int RequestParallelConnections {
			get { return requestParallelConnections; }
			set {
				lock (this) {
					if (requestParallelConnections == value)
						return;
					requestParallelConnections = value;
					Run (); 
				}
			}
		}

		internal string ME {
			get;
		}

		public NewListener (TestContext ctx, HttpServer server)
		{
			TestContext = ctx;
			Server = server;
			SyncRoot = new object ();
			ME = $"[{GetType ().Name}({ID})]";
			connections = new LinkedList<Context> ();
			registry = new Dictionary<string, HttpOperation> ();
			mainLoopEvent = new AsyncManualResetEvent (false);
			cts = new CancellationTokenSource ();

			ctx.LogDebug (5, $"{ME} TEST: {ctx.Result}");
		}

		public void Run ()
		{
			lock (SyncRoot) {
				if (Interlocked.CompareExchange (ref running, 1, 0) == 0)
					MainLoop ();

				mainLoopEvent.Set ();
			}
		}

		void Debug (string message)
		{
			TestContext.LogDebug (5, $"{ME}: {message}");
		}

		public Uri RegisterOperation (TestContext ctx, HttpOperation operation)
		{
			lock (SyncRoot) {
				var id = Interlocked.Increment (ref nextRequestID);
				var path = $"/id/{operation.ID}/{operation.Handler.GetType ().Name}/";
				var uri = new Uri (Server.TargetUri, path);
				registry.Add (path, operation);
				return uri;
			}
		}

		async void MainLoop ()
		{
			while (!disposed) {
				Debug ($"MAIN LOOP");

				var taskList = new List<Task> ();
				var connectionArray = new List<Context> ();
				lock (SyncRoot) {
					RunScheduler ();

					taskList.Add (mainLoopEvent.WaitAsync ());
					foreach (var context in connections) {
						Task task = null;
						switch (context.State) {
						case State.None:
							task = context.Run (TestContext, cts.Token);
							break;
						case State.HasRequest:
							task = context.HandleRequest (TestContext, cts.Token);
							break;
						}
						if (task != null) {
							connectionArray.Add (context);
							taskList.Add (task);
						}
					}
				}

				Debug ($"MAIN LOOP #0: {connectionArray.Count}");

				var ret = await Task.WhenAny (taskList).ConfigureAwait (false);
				Debug ($"MAIN LOOP #1: {ret.Status} {ret == taskList[0]}");

				lock (SyncRoot) {
					if (ret == taskList[0]) {
						mainLoopEvent.Reset ();
						continue;
					}

					int idx = -1;
					for (int i = 0; i < connectionArray.Count; i++) {
						if (ret == taskList[i + 1]) {
							idx = i;
							break;
						}
					}

					bool reuse = false;
					var context = connectionArray[idx];
					Debug ($"MAIN LOOP #2: {context.State} {context.Connection.ME}");
					switch (context.State) {
					case State.Accepted:
						reuse = GetOperation (context, (Task<HttpRequest>)ret);
						break;
					case State.HasRequest:
						reuse = RequestComplete (context, ret);
						break;
					}

					if (!reuse) {
						connections.Remove (context);
						context.Connection.Dispose ();
					}
				}
			}
		}

		void RunScheduler ()
		{
			while (connections.Count < requestParallelConnections) {
				Debug ($"RUN SCHEDULER: {connections.Count}");
				var connection = CreateConnection ();
				connections.AddLast (new Context (this, connection));
				Debug ($"RUN SCHEDULER #1: {connection.ME}");
			}
		}

		bool GetOperation (Context context, Task<HttpRequest> task)
		{
			var me = $"{nameof (GetOperation)}({context.Connection.ME})";
			if (task.Status == TaskStatus.Canceled || task.Status == TaskStatus.Faulted) {
				Debug ($"{me} FAILED: {task.Status} {task.Exception?.Message}");
				return false;
			}

			var request = task.Result;
			Debug ($"{me} {request.Method} {request.Path} {request.Protocol}");

			var operation = registry[request.Path];
			if (operation == null) {
				Debug ($"{me} INVALID PATH: {request.Path}!");
				return false;
			}

			registry.Remove (request.Path);
			context.Operation = operation;
			context.Request = request;
			context.State = State.HasRequest;
			return true;
		}

		bool RequestComplete (Context context, Task task)
		{
			var me = $"{nameof (RequestComplete)}({context.Connection.ME})";
			if (task.Status == TaskStatus.Canceled || task.Status == TaskStatus.Faulted) {
				Debug ($"{me} FAILED: {task.Status} {task.Exception?.Message}");
				return false;
			}

			Debug ($"{me}");

			return false;
		}

		[Obsolete ("KILL")]
		public void Initialize (int numConnections)
		{
			lock (SyncRoot) {
				for (int i = 0; i < numConnections; i++) {
					var connection = CreateConnection ();
					connections.AddLast (new Context (this, connection));
				}
				Run ();
			}
		}

		[Obsolete ("KILL")]
		public void AddConnection ()
		{
			lock (SyncRoot) {
				var connection = CreateConnection ();
				connections.AddLast (new Context (this, connection));
				Run ();
			}
		}

		void CloseAll ()
		{
			lock (SyncRoot) {
				if (closed)
					return;
				closed = true;
				TestContext.LogDebug (5, $"{ME}: CLOSE ALL");

				var iter = connections.First;
				while (iter != null) {
					var node = iter.Value;
					iter = iter.Next;

					node.Connection.Dispose ();
					connections.Remove (node);
				}

				TestContext.LogDebug (5, $"{ME}: CLOSE ALL DONE");
			}
		}

		protected virtual void Shutdown ()
		{
		}

		protected abstract HttpConnection CreateConnection ();

		public void Dispose ()
		{
			lock (SyncRoot) {
				if (disposed)
					return;
				Debug ($"DISPOSE");
				disposed = true;
				cts.Cancel ();
				CloseAll ();
				Shutdown ();
				cts.Dispose ();
				mainLoopEvent.Set ();
			}
		}

		enum State {
			None,
			Accepted,
			HasRequest,
			Closed
		}

		class Context
		{
			public NewListener Listener {
				get;
			}
			public HttpConnection Connection {
				get; set;
			}
			public HttpRequest Request {
				get; set;
			}
			public HttpOperation Operation {
				get; set;
			}
			public State State {
				get; set;
			}

			public Context (NewListener listener, HttpConnection connection)
			{
				Listener = listener;
				Connection = connection;
				State = State.None;
			}

			TaskCompletionSource<HttpRequest> initTask;

			public Task<HttpRequest> Run (TestContext ctx, CancellationToken cancellationToken)
			{
				var tcs = new TaskCompletionSource<HttpRequest> ();
				var old = Interlocked.CompareExchange (ref initTask, tcs, null);
				if (old != null)
					return old.Task;

				Run_inner ().ContinueWith (t => {
					State = State.Accepted;
					if (t.Status == TaskStatus.Canceled)
						tcs.TrySetCanceled ();
					else if (t.Status == TaskStatus.Faulted)
						tcs.TrySetException (t.Exception);
					else
						tcs.TrySetResult (t.Result);
				});

				return tcs.Task;

				async Task<HttpRequest> Run_inner ()
				{
					var me = $"{Listener.ME}({Connection.ME}) RUN";
					cancellationToken.ThrowIfCancellationRequested ();
					await Connection.AcceptAsync (ctx, cancellationToken).ConfigureAwait (false);

					ctx.LogDebug (5, $"{me} #1");

					cancellationToken.ThrowIfCancellationRequested ();
					await Connection.Initialize (ctx, Operation, cancellationToken);

					ctx.LogDebug (5, $"{me} #2");

					var reader = new HttpStreamReader (Connection.SslStream);
					cancellationToken.ThrowIfCancellationRequested ();
					var header = await reader.ReadLineAsync (cancellationToken);
					var (method, protocol, path) = HttpMessage.ReadHttpHeader (header);
					ctx.LogDebug (5, $"{me} #3: {method} {protocol} {path}");

					var request = new HttpRequest (protocol, method, path, reader);
					return request;
				}
			}

			public Task HandleRequest (TestContext ctx, CancellationToken cancellationToken)
			{
				return Operation.HandleRequest (ctx, Connection, Request, cancellationToken);
			}
		}
	}
}
