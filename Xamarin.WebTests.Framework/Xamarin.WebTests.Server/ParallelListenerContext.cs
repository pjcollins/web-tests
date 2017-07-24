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

			ctx.LogDebug (5, $"{me}");

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
					return CreateIteration (Start, Accepted);
				case ConnectionState.KeepAlive:
					return CreateIteration (ReuseConnection, Accepted);
				case ConnectionState.WaitingForRequest:
					return CreateIteration (ReadRequestHeader, GotRequest);
				case ConnectionState.HasRequest:
					return CreateIteration (HandleRequest, RequestComplete);
				default:
					throw ctx.AssertFail (State);
				}
			}

			Task<(bool complete, bool success)> Start ()
			{
				return Initialize (ctx, connection, false, null, cancellationToken);
			}

			Task<(bool complete, bool success)> ReuseConnection ()
			{
				return Initialize (ctx, connection, true, null, cancellationToken);
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
				ctx.LogDebug (5, $"{me}: {keepAlive} {next?.ME}");

				if (next != null) {
					throw new NotImplementedException ();
				}

				if (!keepAlive)
					return ConnectionState.Closed;

				return ConnectionState.KeepAlive;
			}
		}

		public bool MainLoopIterationDone (TestContext ctx, Task task, CancellationToken cancellationToken)
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
			return nextState != ConnectionState.Closed;
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
