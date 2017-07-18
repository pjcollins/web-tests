//
// NewListenerContext.cs
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

	abstract class NewListenerContext : IDisposable
	{
		public NewListener Listener {
			get;
		}

		public HttpServer Server => Listener.Server;

		internal abstract string ME {
			get;
		}

		public NewListenerContext (NewListener listener)
		{
			Listener = listener;
		}

		internal bool HasConnection {
			get;
			private set;
		}

		TaskCompletionSource<HttpRequest> initTask;

		public Task<HttpRequest> Run (TestContext ctx, CancellationToken cancellationToken)
		{
			var tcs = new TaskCompletionSource<HttpRequest> ();
			var old = Interlocked.CompareExchange (ref initTask, tcs, null);
			if (old != null)
				return old.Task;

			Accept (ctx, cancellationToken).ContinueWith (t => {
				HasConnection = true;
				if (t.Status == TaskStatus.Canceled)
					tcs.TrySetCanceled ();
				else if (t.Status == TaskStatus.Faulted)
					tcs.TrySetException (t.Exception);
				else
					tcs.TrySetResult (t.Result);
			});

			return tcs.Task;
		}

		protected abstract Task<HttpRequest> Accept (TestContext ctx, CancellationToken cancellationToken);

		int disposed;

		protected abstract void Close ();

		public void Dispose ()
		{
			if (Interlocked.CompareExchange (ref disposed, 1, 0) != 0)
				return;
			Close ();
		}
	}
}
