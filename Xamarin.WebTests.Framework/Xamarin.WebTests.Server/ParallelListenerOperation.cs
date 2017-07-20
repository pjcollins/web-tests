﻿//
// ParallelListenerOperation.cs
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

	class ParallelListenerOperation : ListenerOperation
	{
		public ParallelListenerOperation (ParallelListener listener, HttpOperation operation, Handler handler, Uri uri)
			: base (listener, operation, handler, uri)
		{
			serverInitTask = new TaskCompletionSource<object> ();
			serverStartTask = new TaskCompletionSource<object> (); 
		}

		TaskCompletionSource<object> serverInitTask;
		TaskCompletionSource<object> serverStartTask;
		HttpConnection redirectRequested;

		public override Task ServerInitTask => serverInitTask.Task;

		public override Task ServerStartTask => serverStartTask.Task;

		new public async Task HandleRequest (TestContext ctx, HttpConnection connection,
		                                     HttpRequest request, CancellationToken cancellationToken)
		{
			serverInitTask.TrySetResult (null);
			try {
				await base.HandleRequest (ctx, connection, request, cancellationToken).ConfigureAwait (false);
				serverStartTask.TrySetResult (null);
			} catch (OperationCanceledException) {
				serverStartTask.TrySetCanceled ();
				throw;
			} catch (Exception ex) {
				serverStartTask.TrySetException (ex);
				throw;
			}
		}

		public override Uri PrepareRedirect (TestContext ctx, Handler handler, bool keepAlive)
		{
			lock (Listener) {
				var me = $"{ME}({nameof (PrepareRedirect)}";
				ctx.LogDebug (5, $"{me}: {handler.Value} {keepAlive}");
				throw new NotImplementedException ();
			}
		}

		public override void PrepareRedirect (TestContext ctx, HttpConnection connection, bool keepAlive)
		{
			lock (Listener) {
				var me = $"{Listener.FormatConnection (connection)} PREPARE REDIRECT";
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
	}
}
