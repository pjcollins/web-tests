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
		public ParallelListenerContext (ParallelListener listener, HttpConnection connection)
			: base (listener)
		{
			this.connection = connection;
			serverInitTask = new TaskCompletionSource<object> ();
			serverStartTask = new TaskCompletionSource<object> ();
			requestTask = new TaskCompletionSource<HttpRequest> ();
		}

		TaskCompletionSource<object> serverInitTask;
		TaskCompletionSource<object> serverStartTask;
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

		public void StartOperation (ParallelListenerOperation operation, HttpRequest request)
		{
			if (Interlocked.CompareExchange (ref currentOperation, operation, null) != null)
				throw new InvalidOperationException ();
			Request = request;
			State = ConnectionState.HasRequest;
		}

		public override void Continue ()
		{
			currentOperation = null;
		}

		public override Task ServerInitTask => serverInitTask.Task;

		public override Task ServerStartTask => serverStartTask.Task;

		public Task<HttpRequest> RequestTask => requestTask.Task;

		public override Task Run (TestContext ctx, CancellationToken cancellationToken)
		{
			throw new NotImplementedException ();
		}

		public Task Start (TestContext ctx, CancellationToken cancellationToken)
		{
			var me = $"{Listener.ME}({Connection.ME}) START";

			var tcs = new TaskCompletionSource<HttpRequest> ();
			if (Interlocked.CompareExchange (ref initialized, 1, 0) != 0)
				throw new InternalErrorException ();

			Start_inner ();
			return serverInitTask.Task;

			void OnCanceled ()
			{
				serverInitTask.TrySetCanceled ();
				serverStartTask.TrySetCanceled ();
				requestTask.TrySetCanceled ();
			}

			void OnError (Exception error)
			{
				serverInitTask.TrySetException (error);
				serverStartTask.TrySetException (error);
				requestTask.TrySetException (error);
			}

			async void Start_inner ()
			{
				try {
					ctx.LogDebug (5, $"{me} ACCEPT");
					cancellationToken.ThrowIfCancellationRequested ();
					await Connection.AcceptAsync (ctx, cancellationToken).ConfigureAwait (false);
					ctx.LogDebug (5, $"{me} ACCEPTED");
					serverInitTask.TrySetResult (null);
				} catch (OperationCanceledException) {
					OnCanceled ();
					return;
				} catch (Exception ex) {
					OnError (ex);
					return;
				}

				try {
					cancellationToken.ThrowIfCancellationRequested ();
					await Connection.Initialize (ctx, null, cancellationToken);
					serverStartTask.TrySetResult (null);
				} catch (OperationCanceledException) {
					OnCanceled ();
					return;
				} catch (Exception ex) {
					OnError (ex);
					return;
				}

				ctx.LogDebug (5, $"{me} #2");

				try {
					var reader = new HttpStreamReader (Connection.SslStream);
					cancellationToken.ThrowIfCancellationRequested ();
					var header = await reader.ReadLineAsync (cancellationToken);
					var (method, protocol, path) = HttpMessage.ReadHttpHeader (header);
					ctx.LogDebug (5, $"{me} #3: {method} {protocol} {path}");

					var request = new HttpRequest (protocol, method, path, reader);
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

		public Task<bool> HandleRequest (TestContext ctx, CancellationToken cancellationToken)
		{
			return Operation.HandleRequest (ctx, Connection, Request, cancellationToken);
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
