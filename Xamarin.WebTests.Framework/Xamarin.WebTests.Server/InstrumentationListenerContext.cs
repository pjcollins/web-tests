//
// InstrumentationListenerContext.cs
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

	class InstrumentationListenerContext : ListenerContext
	{
		new public InstrumentationListener Listener => (InstrumentationListener)base.Listener;

		public InstrumentationListenerContext (Listener listener)
			: base (listener)
		{
			serverInitTask = new TaskCompletionSource<object> ();
		}

		public override HttpConnection Connection {
			get { return currentConnection; }
		}

		HttpConnection redirectRequested;
		HttpOperation currentOperation;
		HttpConnection currentConnection;
		TaskCompletionSource<object> serverInitTask;

		public bool StartOperation (HttpOperation operation)
		{
			return Interlocked.CompareExchange (ref currentOperation, operation, null) == null;
		}

		public override void Continue ()
		{
			currentOperation = null;
		}

		public override Task ServerInitTask => serverInitTask.Task;

		public override async Task Run (TestContext ctx, CancellationToken cancellationToken)
		{
			cancellationToken.ThrowIfCancellationRequested ();

			bool reused;
			HttpConnection connection;
			HttpOperation operation;

			lock (Listener) {
				operation = currentOperation;
				if (operation == null)
					throw new InvalidOperationException ();

				connection = currentConnection;
				if (connection == null) {
					connection = Listener.Backend.CreateConnection ();
					reused = false;
				} else {
					reused = true;
				}
			}

			while (true) {
				var me = $"{ME}({connection.ME}) LOOP";
				ctx.LogDebug (2, $"{me}: {reused}");

				try {
					var (complete, success) = await Initialize (
						ctx, connection, reused, operation, cancellationToken).ConfigureAwait (false);
					if (!complete) {
						connection.Dispose ();
						connection = Listener.Backend.CreateConnection ();
						reused = false;
						continue;
					}
					serverInitTask.TrySetResult (success);
					if (!success) {
						connection.Dispose ();
						return;
					}
				} catch (OperationCanceledException) {
					connection.Dispose ();
					serverInitTask.TrySetCanceled ();
					throw;
				} catch (Exception ex) {
					connection.Dispose ();
					serverInitTask.TrySetException (ex);
					throw;
				}

				ctx.LogDebug (2, $"{me} #1: {reused}");

				var request = await ((SocketConnection)connection).ReadRequestHeader (ctx, cancellationToken);
				ctx.LogDebug (2, $"{me} GOT REQUEST: {request}");

				ListenerOperation listenerOperation;
				lock (Listener) {
					currentConnection = connection;
					listenerOperation = Listener.GetOperation (this, request);
					ctx.LogDebug (2, $"{me} GOT OPERATION: {listenerOperation.ME}");
				}

				bool keepAlive;
				try {
					(keepAlive, _) = await listenerOperation.HandleRequest (
						ctx, this, connection, request, cancellationToken);
				} catch (Exception ex) {
					ctx.LogDebug (2, $"{me} - ERROR {ex.Message}");
					connection.Dispose ();
					throw;
				}

				lock (Listener) {
					var redirect = Interlocked.Exchange (ref redirectRequested, null);
					ctx.LogDebug (2, $"{me} #2: {keepAlive} {redirect?.ME}");

					if (redirect == null) {
						Listener.Continue (ctx, this, keepAlive);
						if (keepAlive)
							currentConnection = connection;
						else
							connection.Dispose ();
						return;
					}

					if (operation.HasAnyFlags (HttpOperationFlags.ClientDoesNotSendRedirect)) {
						connection.Dispose ();
						return;
					}

					reused = redirect == connection;
					if (!reused) {
						connection.Dispose ();
						connection = redirect;
					}
				}
			}

		}

		public override void PrepareRedirect (TestContext ctx, HttpConnection connection, bool keepAlive)
		{
			lock (Listener) {
				var me = $"{ME}({connection.ME}) REDIRECT";
				ctx.LogDebug (5, $"{me}: {keepAlive}");
				HttpConnection next;
				if (keepAlive)
					next = connection;
				else
					next = Listener.Backend.CreateConnection ();

				if (Interlocked.CompareExchange (ref redirectRequested, next, null) != null)
					throw new InvalidOperationException ();
			}
		}

		protected override void Close ()
		{
			if (currentConnection != null) {
				currentConnection.Dispose ();
				currentConnection = null;
			}
		}
	}
}
