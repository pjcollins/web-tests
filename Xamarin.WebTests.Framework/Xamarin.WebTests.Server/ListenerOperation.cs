﻿//
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

	class ListenerOperation
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
		ListenerOperation parentOperation;

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

		internal async Task<HttpResponse> HandleRequest (
			TestContext ctx, ListenerContext context, HttpConnection connection,
			HttpRequest request, CancellationToken cancellationToken)
		{
			var me = $"{ME} HANDLE REQUEST";
			ctx.LogDebug (2, $"{me} {connection.ME} {request}");

			OnInit ();

			HttpResponse response;

			try {
				cancellationToken.ThrowIfCancellationRequested ();
				await request.Read (ctx, cancellationToken).ConfigureAwait (false);

				ctx.LogDebug (2, $"{me} REQUEST FULLY READ");

				response = await Handler.NewHandleRequest (
					ctx, Operation, connection, request, cancellationToken).ConfigureAwait (false);

				ctx.LogDebug (2, $"{me} HANDLE REQUEST DONE: {response}");

			} catch (OperationCanceledException) {
				OnCanceled ();
				throw;
			} catch (Exception ex) {
				OnError (ex);
				throw;
			}

			if (response.Redirect != null) {
				response.Redirect.parentOperation = this;
			} else {
				OnFinished ();
			}

			return response;
		}

		void OnInit ()
		{
			serverInitTask.TrySetResult (null);
			parentOperation?.OnInit ();
		}

		void OnFinished ()
		{
			serverFinishedTask.TrySetResult (null);
			parentOperation?.OnFinished ();
		}

		void OnCanceled ()
		{
			serverInitTask.TrySetCanceled ();
			serverFinishedTask.TrySetCanceled ();
			parentOperation?.OnCanceled ();
		}

		void OnError (Exception error)
		{
			serverInitTask.TrySetException (error);
			serverFinishedTask.TrySetException (error);
			parentOperation?.OnCanceled ();
		}
	}
}
