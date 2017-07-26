//
// ListenerContext.cs
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
using System.Threading;
using System.Threading.Tasks;
using Xamarin.AsyncTests;

namespace Xamarin.WebTests.Server
{
	using HttpFramework;

	class ListenerContext : IDisposable
	{
		public Listener Listener {
			get;
		}

		public HttpServer Server => Listener.Server;

		public ConnectionState State {
			get;
			private set;
		}

		static int nextID;
		public readonly int ID = Interlocked.Increment (ref nextID);

		internal string ME {
			get;
		}

		public ListenerContext (Listener listener, HttpConnection connection, bool reusing)
		{
			this.connection = connection;
			ReusingConnection = reusing;

			Listener = listener;
			State = ConnectionState.Listening;
			ME = $"[{ID}:{GetType ().Name}:{listener.ME}]";

			httpContext = new HttpContext (connection, reusing);
		}

		public HttpConnection Connection {
			get { return connection; }
		}

		public HttpContext HttpContext {
			get { return httpContext; }
		}

		public bool ReusingConnection {
			get;
		}

		public ListenerOperation Operation {
			get { return currentOperation; }
		}

		public ListenerTask CurrentTask {
			get { return currentListenerTask; }
		}

		public bool Listening {
			get;
			private set;
		}

		HttpRequest currentRequest;
		HttpResponse currentResponse;
		ListenerOperation redirectRequested;
		ListenerOperation currentOperation;
		HttpOperation currentInstrumentation;
		HttpConnection connection;
		HttpContext httpContext;
		ListenerTask currentListenerTask;
		ListenerContext redirectContext;

		public bool StartOperation (HttpOperation operation)
		{
			if (!Listener.UsingInstrumentation)
				throw new InvalidOperationException ();

			if (Interlocked.CompareExchange (ref currentInstrumentation, operation, null) != null)
				return false;

			State = ConnectionState.Listening;
			return true;
		}

		internal void Redirect (ListenerContext newContext)
		{
			if (State != ConnectionState.NeedContextForRedirect)
				throw new InvalidOperationException ();

			redirectContext = newContext;
			redirectContext.currentInstrumentation = currentInstrumentation;
			State = ConnectionState.RequestComplete;
		}

		public ListenerTask MainLoopListenerTask (TestContext ctx, CancellationToken cancellationToken)
		{
			var me = $"{Listener.ME}({Connection.ME}) TASK";

			HttpOperation instrumentation;
			lock (Listener) {
				if (currentListenerTask != null)
					throw new InvalidOperationException ();

				instrumentation = currentInstrumentation;
				if (Listener.UsingInstrumentation && instrumentation == null)
					throw new InvalidOperationException ();
			}

			ctx.LogDebug (5, $"{me} {State}");

			currentListenerTask = StartListenerTask ();
			currentListenerTask.Start ();

			return currentListenerTask;

			ListenerTask StartListenerTask ()
			{
				switch (State) {
				case ConnectionState.Listening:
					return ListenerTask.Create (this, State, Start, Accepted);
				case ConnectionState.WaitingForRequest:
					return ListenerTask.Create (this, State, ReadRequestHeader, GotRequest);
				case ConnectionState.HasRequest:
					return ListenerTask.Create (this, State, HandleRequest, RequestComplete);
				case ConnectionState.RequestComplete:
					return ListenerTask.Create (this, State, WriteResponse, ResponseWritten);
				default:
					throw ctx.AssertFail (State);
				}
			}

			Task<(bool complete, bool success)> Start ()
			{
				Listening = true;
				return httpContext.Initialize (ctx, instrumentation, cancellationToken);
			}

			ConnectionState Accepted (bool completed, bool success)
			{
				Listening = false;
				return ConnectionState.WaitingForRequest;
			}

			Task<HttpRequest> ReadRequestHeader ()
			{
				return Connection.ReadRequestHeader (ctx, cancellationToken);
			}

			ConnectionState GotRequest (HttpRequest request)
			{
				var operation = Listener.GetOperation (this, request);
				if (operation == null) {
					ctx.LogDebug (5, $"{me} INVALID REQUEST: {request.Path}");
					return ConnectionState.Closed;
				}
				currentOperation = operation;
				currentRequest = request;
				ctx.LogDebug (5, $"{me} GOT REQUEST");
				return ConnectionState.HasRequest;
			}

			Task<HttpResponse> HandleRequest ()
			{
				return currentOperation.HandleRequest (ctx, this, Connection, currentRequest, cancellationToken);
			}

			ConnectionState RequestComplete (HttpResponse response)
			{
				ctx.LogDebug (5, $"{me}: {response} {response.Redirect?.ME}");

				currentResponse = response;

				var keepAlive = (response.KeepAlive ?? false) && !response.CloseConnection;
				if (response.Redirect != null && !keepAlive)
					return ConnectionState.NeedContextForRedirect;

				return ConnectionState.RequestComplete;
			}

			async Task<bool> WriteResponse ()
			{
				var response = Interlocked.Exchange (ref currentResponse, null);
				var redirect = Interlocked.Exchange (ref redirectContext, null);

				redirectRequested = response.Redirect;

				var keepAlive = (response.KeepAlive ?? false) && !response.CloseConnection;

				if (response.Redirect != null) {
					ctx.LogDebug (5, $"{me} REDIRECT: {keepAlive}");
				}

				if (redirect != null) {
					ctx.LogDebug (5, $"{me} REDIRECT ON NEW CONTEXT: {redirect.ME}!");
					await redirect.httpContext.ServerStartTask.ConfigureAwait (false);
					ctx.LogDebug (5, $"{me} REDIRECT ON NEW CONTEXT #1: {redirect.ME}!");
				}

				await connection.WriteResponse (ctx, response, cancellationToken).ConfigureAwait (false);
				return keepAlive;
			}

			ConnectionState ResponseWritten (bool keepAlive)
			{
				var request = Interlocked.Exchange (ref currentRequest, null);
				var operation = Interlocked.Exchange (ref currentOperation, null);
				var redirect = Interlocked.Exchange (ref redirectRequested, null);

				if (!keepAlive) {
					connection.Dispose ();
					connection = null;
				}

				if (redirect == null)
					return keepAlive ? ConnectionState.ReuseConnection : ConnectionState.Closed;

				currentOperation = redirect;
				if (!keepAlive) {
					return ConnectionState.Closed;
					connection = Listener.Backend.CreateConnection ();
					return ConnectionState.Listening;
				}

				return ConnectionState.WaitingForRequest;
			}
		}

		public void MainLoopListenerTaskDone (TestContext ctx, CancellationToken cancellationToken)
		{
			var me = $"{Listener.ME}({Connection.ME}) TASK DONE";

			var task = Interlocked.Exchange (ref currentListenerTask, null);

			ctx.LogDebug (5, $"{me}: {task.Task.Status} {State}");

			if (task.Task.Status == TaskStatus.Canceled) {
				httpContext.OnCanceled ();
				State = ConnectionState.Closed;
				return;
			}

			if (task.Task.Status == TaskStatus.Faulted) {
				httpContext.OnError (task.Task.Exception);
				State = ConnectionState.Closed;
				return;
			}

			var nextState = task.Continue ();

			ctx.LogDebug (5, $"{me} DONE: {State} -> {nextState}");

			State = nextState;
		}

		internal static Task FailedTask (Exception ex)
		{
			return Listener.FailedTask (ex);
		}

		bool disposed;

		void Close ()
		{
			if (httpContext != null) {
				httpContext.Dispose ();
				httpContext = null;
				connection = null;
			}
			if (connection != null) {
				connection.Dispose ();
				connection = null;
			}
		}

		public ListenerContext ReuseConnection ()
		{
			disposed = true;
			var oldConnection = Interlocked.Exchange (ref connection, null);
			if (oldConnection == null)
				throw new InvalidOperationException ();
			var newContext = new ListenerContext (Listener, oldConnection, true);
			newContext.State = ConnectionState.WaitingForRequest;
			newContext.currentInstrumentation = currentInstrumentation;
			return newContext;
		}

		public void Dispose ()
		{
			if (disposed)
				return;
			disposed = true;
			Close ();
		}
	}
}
