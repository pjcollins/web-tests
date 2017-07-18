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
		LinkedList<NewListenerContext> connections;
		volatile bool disposed;
		volatile bool closed;

		int running;
		CancellationTokenSource cts;
		AsyncManualResetEvent mainLoopEvent;
		Dictionary<string, Handler> handlerRegistration;

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

		internal string ME {
			get;
		}

		public NewListener (TestContext ctx, HttpServer server)
		{
			TestContext = ctx;
			Server = server;
			SyncRoot = new object ();
			ME = $"[{GetType ().Name}({ID})]";
			connections = new LinkedList<NewListenerContext> ();
			handlerRegistration = new Dictionary<string, Handler> ();
			mainLoopEvent = new AsyncManualResetEvent (false);
			cts = new CancellationTokenSource ();
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

		public Uri RegisterHandler (TestContext ctx, Handler handler)
		{
			lock (SyncRoot) {
				var id = Interlocked.Increment (ref nextRequestID);
				var path = $"/id/{handler.GetType ().Name}/";
				var uri = new Uri (Server.TargetUri, path);
				handlerRegistration.Add (path, handler);
				return uri;
			}
		}

		async void MainLoop ()
		{
			while (true) {
				Debug ($"MAIN LOOP");

				var taskList = new List<Task> ();
				var connectionArray = new List<NewListenerContext> ();
				lock (SyncRoot) {
					taskList.Add (mainLoopEvent.WaitAsync ());
					foreach (var connection in connections) {
						if (connection.HasConnection)
							continue;
						connectionArray.Add (connection);
						taskList.Add (connection.Run (TestContext, cts.Token));
					}
				}

				Debug ($"MAIN LOOP #0: {connectionArray.Count}");

				var ret = await Task.WhenAny (taskList).ConfigureAwait (false);
				Debug ($"MAIN LOOP #1: {ret.Status} {ret == taskList[0]}");

				lock (SyncRoot) {
					if (ret == taskList[0]) {
						RunScheduler ();;
						continue;
					}

					int idx = -1;
					for (int i = 0; i < connectionArray.Count; i++) {
						if (ret == taskList[i + 1]) {
							idx = i;
							break;
						}
					}

					HandleConnection (connectionArray[idx], (Task<HttpRequest>)ret);
				}
			}
		}

		void RunScheduler ()
		{
			mainLoopEvent.Reset ();
		}

		void HandleConnection (NewListenerContext connection, Task<HttpRequest> task)
		{
			var me = $"{nameof (HandleConnection)}({connection.ME})";
			if (task.Status == TaskStatus.Canceled || task.Status == TaskStatus.Faulted) {
				Debug ($"{me} FAILED: {task.Status} {task.Exception?.Message}");
				connections.Remove (connection);
				connection.Dispose ();
				return;
			}

			var request = task.Result;
			Debug ($"{me} {request.Method} {request.Path} {request.Protocol}");

			var handler = handlerRegistration[request.Path];
			if (handler == null) {
				Debug ($"{me} INVALID PATH: {request.Path}!");
				connections.Remove (connection);
				connection.Dispose ();
				return;
			}

			Debug ($"{me} {handler}");

			
		}

		public void Initialize (int numConnections)
		{
			lock (SyncRoot) {
				for (int i = 0; i < numConnections; i++) {
					var connection = CreateConnection ();
					connections.AddLast (connection);
				}
				Run ();
			}
		}

		public void AddConnection ()
		{
			lock (SyncRoot) {
				var connection = CreateConnection ();
				connections.AddLast (connection);
				Run ();
			}
		}

		public virtual void CloseAll ()
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

					node.Dispose ();
					connections.Remove (node);
				}

				TestContext.LogDebug (5, $"{ME}: CLOSE ALL DONE");
			}
		}

		protected virtual void Shutdown ()
		{
		}

		protected virtual void OnStop ()
		{
			TestContext.LogDebug (5, "${ME} ON STOP");
			cts.Cancel ();
		}

		protected abstract NewListenerContext CreateConnection ();

		public void Dispose ()
		{
			lock (SyncRoot) {
				if (disposed)
					return;
				disposed = true;
				cts.Cancel ();
				CloseAll ();
				Shutdown ();
				cts.Dispose ();
			}
		}
	}
}
