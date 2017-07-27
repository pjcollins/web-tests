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
using System.IO;
using System.Net;
using System.Net.Sockets;
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

			serverStartTask = new TaskCompletionSource<object> ();
			serverReadyTask = new TaskCompletionSource<object> ();
		}

		public HttpConnection Connection {
			get { return connection; }
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
		SocketConnection targetConnection;
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
			if (State == ConnectionState.NeedContextForRedirect) {
				redirectContext = newContext;
				redirectContext.currentInstrumentation = currentInstrumentation;
				State = ConnectionState.RequestComplete;
			} else if (State == ConnectionState.CannotReuseConnection) {
				newContext.currentInstrumentation = currentInstrumentation;
				State = ConnectionState.Closed;
			} else {
				throw new InvalidOperationException ();
			}
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
				case ConnectionState.ConnectToTarget:
					return ListenerTask.Create (this, State, ConnectToTarget, ProxyConnectionEstablished);
				case ConnectionState.HandleProxyConnection:
					return ListenerTask.Create (this, State, HandleProxyConnection, ProxyConnectionFinished);
				default:
					throw ctx.AssertFail (State);
				}
			}

			Task<(bool complete, bool success)> Start ()
			{
				Listening = true;
				return Initialize (ctx, instrumentation, cancellationToken);
			}

			ConnectionState Accepted (bool completed, bool success)
			{
				ctx.LogDebug (5, $"{me} ACCEPTED: {completed} {success}");
				Listening = false;

				if (!completed)
					return ConnectionState.CannotReuseConnection;

				if (!success)
					return ConnectionState.Closed;

				return ConnectionState.WaitingForRequest;
			}

			Task<HttpRequest> ReadRequestHeader ()
			{
				ctx.LogDebug (5, $"{me} READ REQUEST HEADER");
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

				if (Listener.TargetListener != null)
					return ConnectionState.ConnectToTarget;

				return ConnectionState.HasRequest;
			}

			Task<HttpResponse> HandleRequest ()
			{
				return currentOperation.HandleRequest (ctx, this, Connection, currentRequest, cancellationToken);
			}

			async Task ConnectToTarget ()
			{
				ctx.LogDebug (5, $"{me} CONNECT TO TARGET");

				targetConnection = new SocketConnection (Listener.TargetListener.Server);
				var targetEndPoint = new DnsEndPoint (currentOperation.Uri.Host, currentOperation.Uri.Port);
				ctx.LogDebug (5, $"{me} CONNECT TO TARGET #1: {targetEndPoint}");

				cancellationToken.ThrowIfCancellationRequested ();
				await targetConnection.ConnectAsync (ctx, targetEndPoint, cancellationToken);

				ctx.LogDebug (5, $"{me} CONNECT TO TARGET #2");

				cancellationToken.ThrowIfCancellationRequested ();
				await targetConnection.Initialize (ctx, currentOperation.Operation, cancellationToken);

				ctx.LogDebug (5, $"{me} CONNECT TO TARGET #3");
			}

			ConnectionState ProxyConnectionEstablished ()
			{
				return ConnectionState.HandleProxyConnection;
			}

			async Task<HttpResponse> HandleProxyConnection ()
			{
				var copyResponseTask = CopyResponse ();

				cancellationToken.ThrowIfCancellationRequested ();
				await targetConnection.WriteRequest (ctx, currentRequest, cancellationToken);

				ctx.LogDebug (5, $"{me} HANDLE PROXY CONNECTION");

				cancellationToken.ThrowIfCancellationRequested ();
				var response = await copyResponseTask.ConfigureAwait (false);

				ctx.LogDebug (5, $"{me} HANDLE PROXY CONNECTION #1");
				return response;
			}

			ConnectionState ProxyConnectionFinished (HttpResponse response)
			{
				currentResponse = response;
				targetConnection.Dispose ();
				targetConnection = null;
				return ConnectionState.RequestComplete;
			}

			async Task<HttpResponse> CopyResponse ()
			{
				await Task.Yield ();

				cancellationToken.ThrowIfCancellationRequested ();
				var response = await targetConnection.ReadResponse (ctx, cancellationToken).ConfigureAwait (false);
				response.SetHeader ("Connection", "close");
				response.SetHeader ("Proxy-Connection", "close");

				return response;
			}

			ConnectionState RequestComplete (HttpResponse response)
			{
				ctx.LogDebug (5, $"{me}: {response} {response.Redirect?.ME}");

				currentResponse = response;

				var keepAlive = (response.KeepAlive ?? false) && !response.CloseConnection;
				if (response.Redirect != null && !keepAlive && Listener.UsingInstrumentation)
					return ConnectionState.NeedContextForRedirect;

				return ConnectionState.RequestComplete;
			}

			async Task<bool> WriteResponse ()
			{
				var response = Interlocked.Exchange (ref currentResponse, null);
				var redirect = Interlocked.Exchange (ref redirectContext, null);

				redirectRequested = response.Redirect;

				var keepAlive = (response.KeepAlive ?? false) && !response.CloseConnection;

				if (redirect != null) {
					ctx.LogDebug (5, $"{me} REDIRECT ON NEW CONTEXT: {redirect.ME}!");
					await redirect.ServerStartTask.ConfigureAwait (false);
					ctx.LogDebug (5, $"{me} REDIRECT ON NEW CONTEXT #1: {redirect.ME}!");
					keepAlive = false;
				}

				await connection.WriteResponse (ctx, response, cancellationToken).ConfigureAwait (false);
				return keepAlive;
			}

			ConnectionState ResponseWritten (bool keepAlive)
			{
				var request = Interlocked.Exchange (ref currentRequest, null);
				var operation = Interlocked.Exchange (ref currentOperation, null);
				var redirect = Interlocked.Exchange (ref redirectRequested, null);

				if (!keepAlive)
					return ConnectionState.Closed;

				if (redirect == null)
					return ConnectionState.ReuseConnection;

				return ConnectionState.WaitingForRequest;
			}
		}

		public void MainLoopListenerTaskDone (TestContext ctx, CancellationToken cancellationToken)
		{
			var me = $"{Listener.ME}({Connection.ME}) TASK DONE";

			var task = Interlocked.Exchange (ref currentListenerTask, null);

			ctx.LogDebug (5, $"{me}: {task.Task.Status} {State}");

			if (task.Task.Status == TaskStatus.Canceled) {
				OnCanceled ();
				State = ConnectionState.Closed;
				return;
			}

			if (task.Task.Status == TaskStatus.Faulted) {
				OnError (task.Task.Exception);
				State = ConnectionState.Closed;
				return;
			}

			var nextState = task.Continue ();

			ctx.LogDebug (5, $"{me} DONE: {State} -> {nextState}");

			State = nextState;
		}

		TaskCompletionSource<object> serverReadyTask;
		TaskCompletionSource<object> serverStartTask;

		public Task ServerStartTask => serverStartTask.Task;

		public Task ServerReadyTask => serverReadyTask.Task;

		public async Task<(bool complete, bool success)> Initialize (
			TestContext ctx, HttpOperation operation, CancellationToken cancellationToken)
		{
			try {
				ctx.LogDebug (2, $"{ME} INIT");
				(bool complete, bool success) result;
				if (ReusingConnection) {
					if (await ReuseConnection (ctx, operation, cancellationToken).ConfigureAwait (false))
						result = (true, true);
					else
						result = (false, false);
				} else {
					if (await InitConnection (ctx, operation, cancellationToken).ConfigureAwait (false))
						result = (true, true);
					else
						result = (true, false);
				}
				serverReadyTask.TrySetResult (null);
				return result;
			} catch (OperationCanceledException) {
				OnCanceled ();
				throw;
			} catch (Exception ex) {
				ctx.LogDebug (2, $"{ME} INIT FAILED: {ex.Message}");
				OnError (ex);
				throw;
			}
		}

		internal void OnCanceled ()
		{
			serverStartTask.TrySetCanceled ();
			serverReadyTask.TrySetCanceled ();
		}

		internal void OnError (Exception error)
		{
			serverStartTask.TrySetException (error);
			serverReadyTask.TrySetResult (error);
		}

		async Task<bool> ReuseConnection (TestContext ctx, HttpOperation operation, CancellationToken cancellationToken)
		{
			var me = $"{ME}({connection.ME}) REUSE";
			ctx.LogDebug (2, $"{me}");

			serverStartTask.TrySetResult (null);

			cancellationToken.ThrowIfCancellationRequested ();
			var reusable = await connection.ReuseConnection (ctx, cancellationToken).ConfigureAwait (false);

			ctx.LogDebug (2, $"{me} #1: {reusable}");

			if (reusable && (operation?.HasAnyFlags (HttpOperationFlags.ClientUsesNewConnection) ?? false)) {
				try {
					await connection.ReadRequest (ctx, cancellationToken).ConfigureAwait (false);
					throw ctx.AssertFail ("Expected client to use a new connection.");
				} catch (OperationCanceledException) {
					throw;
				} catch (Exception ex) {
					ctx.LogDebug (2, $"{ME} EXPECTED EXCEPTION: {ex.GetType ()} {ex.Message}");
				}
				connection.Dispose ();
				return false;
			}

			return reusable;
		}

		async Task<bool> InitConnection (TestContext ctx, HttpOperation operation, CancellationToken cancellationToken)
		{
			var me = $"{ME}({connection.ME}) INIT";
			ctx.LogDebug (2, $"{me}");

			cancellationToken.ThrowIfCancellationRequested ();
			var acceptTask = connection.AcceptAsync (ctx, cancellationToken);

			serverStartTask.TrySetResult (null);

			await acceptTask.ConfigureAwait (false);

			ctx.LogDebug (2, $"{me} ACCEPTED {connection.RemoteEndPoint}");

			bool haveRequest;

			cancellationToken.ThrowIfCancellationRequested ();
			try {
				await connection.Initialize (ctx, operation, cancellationToken);
				ctx.LogDebug (2, $"{me} #1 {connection.RemoteEndPoint}");

				if (operation != null && operation.HasAnyFlags (HttpOperationFlags.ServerAbortsHandshake))
					throw ctx.AssertFail ("Expected server to abort handshake.");

				/*
				 * There seems to be some kind of a race condition here.
				 *
				 * When the client aborts the handshake due the a certificate validation failure,
				 * then we either receive an exception during the TLS handshake or the connection
				 * will be closed when the handshake is completed.
				 *
				 */
				haveRequest = await connection.HasRequest (cancellationToken);
				ctx.LogDebug (2, $"{me} #2 {haveRequest}");

				if (operation != null && operation.HasAnyFlags (HttpOperationFlags.ClientAbortsHandshake))
					throw ctx.AssertFail ("Expected client to abort handshake.");
			} catch (Exception ex) {
				if (operation.HasAnyFlags (HttpOperationFlags.ServerAbortsHandshake, HttpOperationFlags.ClientAbortsHandshake))
					return false;
				ctx.LogDebug (2, $"{me} FAILED: {ex.Message}\n{ex}");
				throw;
			}

			if (!haveRequest) {
				ctx.LogMessage ($"{me} got empty requets!");
				throw ctx.AssertFail ("Got empty request.");
			}

			if (Server.UseSSL) {
				ctx.Assert (connection.SslStream.IsAuthenticated, "server is authenticated");
				if (operation != null && operation.HasAnyFlags (HttpOperationFlags.RequireClientCertificate))
					ctx.Assert (connection.SslStream.IsMutuallyAuthenticated, "server is mutually authenticated");
			}

			ctx.LogDebug (2, $"{me} DONE");
			return true;
		}

		internal static Task FailedTask (Exception ex)
		{
			return Listener.FailedTask (ex);
		}

		bool disposed;

		void Close ()
		{
			if (connection != null) {
				connection.Dispose ();
				connection = null;
			}
			if (targetConnection != null) {
				targetConnection.Dispose ();
				targetConnection = null;
			}
		}

		public ListenerContext ReuseConnection ()
		{
			disposed = true;
			var oldConnection = Interlocked.Exchange (ref connection, null);
			if (oldConnection == null)
				throw new InvalidOperationException ();

			var newContext = new ListenerContext (Listener, oldConnection, true) {
				State = Listener.UsingInstrumentation ? ConnectionState.WaitingForContext : ConnectionState.WaitingForRequest
			};

			currentInstrumentation = null;
			currentOperation = null;
			State = ConnectionState.Closed;
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
