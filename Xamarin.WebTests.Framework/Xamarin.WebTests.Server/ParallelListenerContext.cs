//
// ParallelListenerContext.cs
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
using System.Collections.Generic;
using Xamarin.AsyncTests;

namespace Xamarin.WebTests.Server
{
	using ConnectionFramework;
	using HttpFramework;

	class ParallelListenerContext : ListenerContext
	{
		new public ParallelListener Listener => (ParallelListener)base.Listener;

		public ParallelListenerContext (ParallelListener listener, HttpConnection connection)
			: base (listener)
		{
			this.connection = connection;
			serverInitTask = new TaskCompletionSource<object> ();
			requestTask = new TaskCompletionSource<HttpRequest> ();
		}

		TaskCompletionSource<object> serverInitTask;
		TaskCompletionSource<HttpRequest> requestTask;
		int initialized;

		public override HttpConnection Connection {
			get { return connection; }
		}

		public ParallelListenerOperation Operation {
			get { return currentOperation; }
		}

		public HttpRequest Request {
			get;
			private set;
		}

		ParallelListenerOperation currentOperation;
		HttpConnection connection;
		Iteration currentIteration;
		Task currentTask;

#if FIXME
		public void StartOperation (ParallelListenerOperation operation, HttpRequest request)
		{
			if (Interlocked.CompareExchange (ref currentOperation, operation, null) != null)
				throw new InvalidOperationException ();
			Request = request;
		}
#endif

		public override void Continue ()
		{
			currentOperation = null;
			connection = null;
			Request = null;
		}

		public override Task ServerInitTask => ServerReadyTask;

		public Task<HttpRequest> RequestTask => requestTask.Task;

		public override Task Run (TestContext ctx, CancellationToken cancellationToken)
		{
			throw new NotImplementedException ();
		}

		public Task MainLoopIteration (TestContext ctx, CancellationToken cancellationToken)
		{
			var me = $"{Listener.ME}({Connection.ME}) ITERATION";

			if (currentTask != null)
				return currentTask;

			ctx.LogDebug (5, "${me}");

			try {
				currentIteration = StartIteration ();
			} catch (Exception ex) {
				currentTask = FailedTask (ex);
			}

			currentTask = currentIteration.Task;
			return currentTask;

			Iteration StartIteration ()
			{
				switch (State) {
				case ConnectionState.None:
					// Start (ctx, false, cancellationToken);
					// State = ConnectionState.Listening;
					// return ServerReadyTask;
					return CreateIteration (Start, Accepted);
#if FIXME
				case ConnectionState.KeepAlive:
					Start (ctx, true, cancellationToken);
					State = ConnectionState.Accepted;
					return ServerStartTask;
				case ConnectionState.Listening:
					return ServerInitTask;
				case ConnectionState.Accepted:
					return ServerStartTask;
#endif
				case ConnectionState.WaitingForRequest:
					return CreateIteration (ReadRequestHeader, GotRequest);
;				case ConnectionState.HasRequest:
					return CreateIteration (HandleRequest, RequestComplete);
				default:
					throw ctx.AssertFail (State);
				}
			}

			Task<(bool complete, bool success)> Start ()
			{
				return Initialize (ctx, connection, false, null, cancellationToken);
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
				var operation = (ParallelListenerOperation)Listener.GetOperation (this, request);
				if (operation == null) {
					ctx.LogDebug (5, $"{me} INVALID REQUEST: {request.Path}");
					// connections.Remove (context);
					// context.Dispose ();
					return ConnectionState.Closed;
				}
				currentOperation = operation;
				Request = request;
				ctx.LogDebug (5, $"{me} GOT REQUEST");
				return ConnectionState.HasRequest;
			}

			Task<(bool keepAlive, ListenerOperation next)> HandleRequest ()
			{
				return Operation.HandleRequest (ctx, this, Connection, Request, cancellationToken);
			}

			ConnectionState RequestComplete (bool keepAlive, ListenerOperation next)
			{
				throw new NotImplementedException ();
			}


		}

		public void MainLoopIterationDone (TestContext ctx, Task task, CancellationToken cancellationToken)
		{
			var me = $"{Listener.ME}({Connection.ME}) ITERATION DONE";

			if (task != currentTask)
				throw new InvalidOperationException ();
			currentTask = null;

			ctx.LogDebug (5, $"{me}: {State}");

			var iteration = Interlocked.Exchange (ref currentIteration, null);
			var nextState = iteration.Continue ();

			ctx.LogDebug (5, $"{me} DONE: {State} -> {nextState}");

			State = nextState;
			return;

			switch (State) {
			case ConnectionState.Listening:
				State = ConnectionState.Accepted;
				break;
			case ConnectionState.Accepted:
				State = ConnectionState.WaitingForRequest;
				break;
			case ConnectionState.WaitingForRequest:
				GotRequest ();
				break;
			case ConnectionState.HasRequest:
				RequestComplete ();
				break;
			}

			void GotRequest ()
			{
				var request = ((Task<HttpRequest>)task).Result;
				var operation = (ParallelListenerOperation)Listener.GetOperation (this, request);
				if (operation == null) {
					ctx.LogDebug (5, $"{me} INVALID REQUEST: {request.Path}");
					// connections.Remove (context);
					// context.Dispose ();
					return;
				}
				currentOperation = operation;
				Request = request;
				State = ConnectionState.HasRequest;
				ctx.LogDebug (5, $"{me} GOT REQUEST");
			}

			void RequestComplete ()
			{
				var (keepAlive, next) = ((Task<(bool, ListenerOperation)>)task).Result;
				ctx.LogDebug (5, $"{me}: {keepAlive} {next?.ME}");

				if (!keepAlive) {
					// connections.Remove (context);
					// context.Dispose ();
					return;
				}

				if (next != null) {
					throw new NotImplementedException ();
				}

				var newContext = new ParallelListenerContext (Listener, Connection);
				newContext.State = ConnectionState.KeepAlive;
				// connections.AddLast (newContext);

				// connections.Remove (context);
				// context.Continue ();
			}
		}

		static Iteration CreateIteration<T> (Func<Task<T>> start, Func<T, ConnectionState> continuation)
		{
			return new Iteration<T> (start, continuation);
		}

		static Iteration CreateIteration<T,U> (Func<Task<(T,U)>> start, Func<T, U, ConnectionState> continuation)
		{
			return new Iteration<T, U> (start, continuation);
		}

		abstract class Iteration
		{
			public abstract Task Task {
				get;
			}

			public abstract ConnectionState Continue ();
		}

		class Iteration<T> : Iteration
		{
			public Task<T> Start {
				get;
			}

			public override Task Task => Start;

			public Func<T, ConnectionState> Continuation {
				get;
			}

			public Iteration (Func<Task<T>> start, Func<T, ConnectionState> continuation)
			{
				Start = start ();
				Continuation = continuation;
			}

			public override ConnectionState Continue ()
			{
				var result = Start.Result;
				return Continuation (result);
			}
		}

		class Iteration<T,U> : Iteration
		{
			public Task<(T,U)> Start {
				get;
			}

			public override Task Task => Start;

			public Func<T, U, ConnectionState> Continuation {
				get;
			}

			public Iteration (Func<Task<(T, U)>> start, Func<T, U, ConnectionState> continuation)
			{
				Start = start ();
				Continuation = continuation;
			}

			public override ConnectionState Continue ()
			{
				var (first, second) = Start.Result;
				return Continuation (first, second);
			}
		}

		void GotRequest (TestContext ctx, HttpRequest request, CancellationToken cancellationToken)
		{
			
		}

		public Task Start (TestContext ctx, bool reused, CancellationToken cancellationToken)
		{
			var me = $"{Listener.ME}({Connection.ME}) START";

			var tcs = new TaskCompletionSource<HttpRequest> ();
			if (Interlocked.CompareExchange (ref initialized, 1, 0) != 0)
				throw new InternalErrorException ();

			Start_inner ();
			return ServerReadyTask;

			async void Start_inner ()
			{
				try {
					ctx.LogDebug (5, $"{me}");
					cancellationToken.ThrowIfCancellationRequested ();
					var (complete, success) = await Initialize (
						ctx, connection, reused, null, cancellationToken).ConfigureAwait (false);
				} catch (OperationCanceledException) {
					OnCanceled ();
					return;
				} catch (Exception ex) {
					OnError (ex);
					return;
				}

				ctx.LogDebug (5, $"{me} #2: {connection.ME} {((SocketConnection)connection).RemoteEndPoint}");
				return;

				try {
					var request = await Connection.ReadRequestHeader (ctx, cancellationToken).ConfigureAwait (false);
					requestTask.TrySetResult (request);
				} catch (OperationCanceledException) {
					OnCanceled ();
					return;
				} catch (Exception ex) {
					OnError (ex);
					return;
				}
			}
		}

		public async Task<HttpRequest> ReadRequest (TestContext ctx, CancellationToken cancellationToken)
		{
			var request = await Connection.ReadRequestHeader (ctx, cancellationToken).ConfigureAwait (false);
			return request;
		}

		protected override void OnCanceled ()
		{
			base.OnCanceled ();
			requestTask.TrySetCanceled ();
		}

		protected override void OnError (Exception error)
		{
			base.OnError (error);
			requestTask.TrySetException (error);
		}

		public Task<(bool keepAlive, ListenerOperation next)> HandleRequest (TestContext ctx, CancellationToken cancellationToken)
		{
			return Operation.HandleRequest (ctx, this, Connection, Request, cancellationToken);
		}

		public override void PrepareRedirect (TestContext ctx, HttpConnection connection, bool keepAlive)
		{
			throw new NotImplementedException ();
		}

		protected override void Close ()
		{
			if (connection != null) {
				connection.Dispose ();
				connection = null;
			}
		}
	}
}
