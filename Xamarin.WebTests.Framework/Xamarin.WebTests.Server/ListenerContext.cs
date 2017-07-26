﻿//
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

		public ListenerContext (Listener listener, HttpConnection connection)
		{
			this.connection = connection;

			Listener = listener;
			State = ConnectionState.None;
			ME = $"[{ID}:{GetType ().Name}:{listener.ME}]";

			serverStartTask = new TaskCompletionSource<object> ();
			serverReadyTask = new TaskCompletionSource<object> ();
			State = ConnectionState.Listening;
		}

		public HttpConnection Connection {
			get { return connection; }
		}

		public ListenerOperation Operation {
			get { return currentOperation; }
		}

		HttpRequest currentRequest;
		HttpResponse currentResponse;
		ListenerOperation redirectRequested;
		ListenerOperation currentOperation;
		HttpOperation currentInstrumentation;
		HttpConnection connection;
		ListenerTask currentListenerTask;

		public bool StartOperation (HttpOperation operation)
		{
			if (!Listener.UsingInstrumentation)
				throw new InvalidOperationException ();

			if (Interlocked.CompareExchange (ref currentInstrumentation, operation, null) != null)
				return false;

			State = ConnectionState.Listening;
			return true;
		}

		public Task MainLoopListenerTask (TestContext ctx, CancellationToken cancellationToken)
		{
			var me = $"{Listener.ME}({Connection.ME}) TASK";

			HttpOperation instrumentation;
			lock (Listener) {
				if (currentListenerTask != null)
					return currentListenerTask.Task;

				instrumentation = currentInstrumentation;
				if (Listener.UsingInstrumentation && instrumentation == null)
					throw new InvalidOperationException ();
			}

			ctx.LogDebug (5, $"{me} {State}");

			try {
				currentListenerTask = StartListenerTask ();
			} catch (Exception ex) {
				return FailedTask (ex);
			}

			return currentListenerTask.Task;

			ListenerTask StartListenerTask ()
			{
				switch (State) {
				case ConnectionState.Listening:
					return ListenerTask.Create (Start, Accepted);
				case ConnectionState.ReuseConnection:
					return ListenerTask.Create (ReuseConnection, Accepted);
				case ConnectionState.WaitingForRequest:
					return ListenerTask.Create (ReadRequestHeader, GotRequest);
				case ConnectionState.HasRequest:
					return ListenerTask.Create (HandleRequest, RequestComplete);
				case ConnectionState.RequestComplete:
					return ListenerTask.Create (WriteResponse, ResponseWritten);
				default:
					throw ctx.AssertFail (State);
				}
			}

			Task<(bool complete, bool success)> Start ()
			{
				return Initialize (ctx, false, instrumentation, cancellationToken);
			}

			Task<(bool complete, bool success)> ReuseConnection ()
			{
				return Initialize (ctx, true, instrumentation, cancellationToken);
			}

			ConnectionState Accepted (bool completed, bool success)
			{
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
				return ConnectionState.RequestComplete;
			}

			async Task<bool> WriteResponse ()
			{
				var response = Interlocked.Exchange (ref currentResponse, null);
				redirectRequested = response.Redirect;

				var keepAlive = (response.KeepAlive ?? false) && !response.CloseConnection;

				if (response.Redirect != null) {
					ctx.LogDebug (5, $"{me} REDIRECT: {keepAlive}");
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
					connection = Listener.Backend.CreateConnection ();
					return ConnectionState.Listening;
				}

				return ConnectionState.WaitingForRequest;
			}
		}

		public ConnectionState MainLoopListenerTaskDone (TestContext ctx, Task task, CancellationToken cancellationToken)
		{
			var me = $"{Listener.ME}({Connection.ME}) task DONE";

			ctx.LogDebug (5, $"{me}: {task.Status} {State}");

			if (task.Status == TaskStatus.Canceled) {
				OnCanceled ();
				State = ConnectionState.Closed;
				return ConnectionState.Closed;
			}

			if (task.Status == TaskStatus.Faulted) {
				OnError (task.Exception);
				State = ConnectionState.Closed;
				return ConnectionState.Closed;
			}

			var currentTask = Interlocked.Exchange (ref currentListenerTask, null);
			if (currentTask.Task != task)
				throw new InvalidOperationException ();

			var nextState = currentTask.Continue ();

			ctx.LogDebug (5, $"{me} DONE: {State} -> {nextState}");

			State = nextState;
			return nextState;
		}

		TaskCompletionSource<object> serverReadyTask;
		TaskCompletionSource<object> serverStartTask;

		public Task ServerStartTask => serverStartTask.Task;

		public Task ServerReadyTask => serverReadyTask.Task;

		async Task<(bool complete, bool success)> Initialize (
			TestContext ctx, bool reused, HttpOperation operation, CancellationToken cancellationToken)
		{
			try {
				(bool complete, bool success) result;
				if (reused) {
					if (await ReuseConnection (ctx, cancellationToken).ConfigureAwait (false))
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
				OnError (ex);
				throw;
			}
		}

		void OnCanceled ()
		{
			serverStartTask.TrySetCanceled ();
			serverReadyTask.TrySetCanceled ();
		}

		void OnError (Exception error)
		{
			serverStartTask.TrySetException (error);
			serverReadyTask.TrySetResult (error);
		}

		async Task<bool> ReuseConnection (TestContext ctx, CancellationToken cancellationToken)
		{
			var me = $"{ME}({connection.ME}) REUSE";
			ctx.LogDebug (2, $"{me}");

			serverStartTask.TrySetResult (null);

			cancellationToken.ThrowIfCancellationRequested ();
			var reusable = await connection.ReuseConnection (ctx, cancellationToken).ConfigureAwait (false);

			ctx.LogDebug (2, $"{me} #1: {reusable}");
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

			if (Listener.Server.UseSSL) {
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
		}

		public ListenerContext ReuseConnection ()
		{
			disposed = true;
			var oldConnection = Interlocked.Exchange (ref connection, null);
			if (oldConnection == null)
				throw new InvalidOperationException ();
			var newContext = new ListenerContext (Listener, oldConnection);
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
