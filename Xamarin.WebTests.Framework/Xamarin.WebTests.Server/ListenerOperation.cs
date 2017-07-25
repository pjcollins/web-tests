//
// ListenerOperation.cs
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
	using HttpHandlers;

	abstract class ListenerOperation
	{
		public Listener Listener {
			get;
		}

		public HttpOperation Operation {
			get;
		}

		public Handler Handler {
			get;
		}

		public Uri Uri {
			get;
		}

		internal string ME {
			get;
		}

		static int nextID;
		public readonly int ID = Interlocked.Increment (ref nextID);

		TaskCompletionSource<object> serverInitTask;
		TaskCompletionSource<object> serverFinishedTask;

		public ListenerOperation (Listener listener, HttpOperation operation, Handler handler, Uri uri)
		{
			Listener = listener;
			Operation = operation;
			Handler = handler;
			Uri = uri;

			ME = $"[{ID}:{GetType ().Name}:{operation.ME}]";
			serverInitTask = new TaskCompletionSource<object> ();
			serverFinishedTask = new TaskCompletionSource<object> ();
		}

		public Task ServerInitTask => serverInitTask.Task;

		public Task ServerFinishedTask => serverFinishedTask.Task;

		internal async Task<(bool keepAlive, ListenerOperation redirect, HttpConnection next)> HandleRequest (
			TestContext ctx, ListenerContext context, HttpConnection connection,
			HttpRequest request, CancellationToken cancellationToken)
		{
			var me = $"{ME} HANDLE REQUEST";
			ctx.LogDebug (2, $"{me} {connection.ME} {request}");

			serverInitTask.TrySetResult (null);

			bool keepAlive;

			try {
				cancellationToken.ThrowIfCancellationRequested ();
				await request.Read (ctx, cancellationToken).ConfigureAwait (false);

				ctx.LogDebug (2, $"{me} REQUEST FULLY READ");

				keepAlive = await Listener.Server.HandleConnection (
					ctx, Operation, connection, request, Handler, cancellationToken).ConfigureAwait (false);

				ctx.LogDebug (2, $"{me} HANDLE REQUEST DONE: {keepAlive}");

			} catch (OperationCanceledException) {
				serverFinishedTask.TrySetCanceled ();
				throw;
			} catch (Exception ex) {
				serverFinishedTask.TrySetException (ex);
				throw;
			}

			ListenerOperation redirect;
			HttpConnection next;
			lock (Listener) {
				redirect = Interlocked.Exchange (ref redirectOperation, null);
				next = Interlocked.Exchange (ref redirectRequested, null);

				ctx.LogDebug (2, $"{me} HANDLE REQUEST DONE #1: {redirect?.ME}");
			}

			serverFinishedTask.TrySetResult (null);
			return (keepAlive, redirect, next);
		}

		ListenerOperation redirectOperation;
		HttpConnection redirectRequested;

		public void PrepareRedirect (TestContext ctx, ListenerOperation redirect,
		                             HttpConnection connection, bool keepAlive)
		{
			lock (Listener) {
				var me = $"{ME}({nameof (PrepareRedirect)}";
				ctx.LogDebug (5, $"{me}: {redirect.ME} {keepAlive}");

				HttpConnection next;
				if (keepAlive)
					next = connection;
				else
					next = Listener.Backend.CreateConnection ();

				if (Interlocked.CompareExchange (ref redirectRequested, next, null) != null)
					throw new InvalidOperationException ();

				redirectOperation = redirect;
			}
		}

		public abstract void PrepareRedirect (TestContext ctx, HttpConnection connection, bool keepAlive);
	}
}
