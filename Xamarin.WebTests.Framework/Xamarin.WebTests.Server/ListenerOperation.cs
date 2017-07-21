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

		public ListenerOperation (Listener listener, HttpOperation operation, Handler handler, Uri uri)
		{
			Listener = listener;
			Operation = operation;
			Handler = handler;
			Uri = uri;

			ME = $"[{ID}:{GetType ().Name}:{operation.ME}]";
		}

		public abstract Task ServerInitTask {
			get;
		}

		public abstract Task ServerFinishedTask {
			get;
		}

		internal async Task<bool> HandleRequest (
			TestContext ctx, HttpConnection connection,
			HttpRequest request, CancellationToken cancellationToken)
		{
			var me = $"{ME} HANDLE REQUEST";
			ctx.LogDebug (2, $"{me} {connection.ME} {request}");

			cancellationToken.ThrowIfCancellationRequested ();
			await request.Read (ctx, cancellationToken).ConfigureAwait (false);

			ctx.LogDebug (2, $"{me} REQUEST FULLY READ");
			var ret = await Handler.HandleRequest (ctx, Operation, connection, request, cancellationToken);
			ctx.LogDebug (2, $"{me} HANDLE REQUEST DONE: {ret}");

			return ret;
		}

		public abstract void PrepareRedirect (TestContext ctx, HttpConnection connection, bool keepAlive);

		public abstract Uri PrepareRedirect (TestContext ctx, Handler handler, bool keepAlive);
	}
}
