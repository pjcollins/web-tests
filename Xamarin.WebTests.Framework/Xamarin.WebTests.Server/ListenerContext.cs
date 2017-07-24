//
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

	abstract class ListenerContext : IDisposable
	{
		public Listener Listener {
			get;
		}

		public HttpServer Server => Listener.Server;

		public abstract HttpConnection Connection {
			get;
		}

		public ConnectionState State {
			get;
			internal set;
		}

		static int nextID;
		public readonly int ID = Interlocked.Increment (ref nextID);

		internal string ME {
			get;
		}

		public ListenerContext (Listener listener)
		{
			Listener = listener;
			State = ConnectionState.None;
			ME = $"[{ID}:{GetType ().Name}:{listener.ME}]";

			serverStartTask = new TaskCompletionSource<object> ();
			serverReadyTask = new TaskCompletionSource<object> ();
		}

		TaskCompletionSource<object> serverReadyTask;
		TaskCompletionSource<object> serverStartTask;

		public abstract void Continue ();

		public abstract Task ServerInitTask {
			get;
		}

		public Task ServerStartTask => serverStartTask.Task;

		public Task ServerReadyTask => serverReadyTask.Task;

		protected async Task<(bool complete, bool success)> Initialize (
			TestContext ctx, HttpConnection connection, bool reused,
			HttpOperation operation, CancellationToken cancellationToken)
		{
			try {
				(bool complete, bool success) result;
				if (reused) {
					if (await ReuseConnection (ctx, connection, cancellationToken).ConfigureAwait (false))
						result = (true, true);
					else
						result = (false, false);
				} else {
					if (await InitConnection (ctx, operation, connection, cancellationToken).ConfigureAwait (false))
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

		protected virtual void OnCanceled ()
		{
			serverStartTask.TrySetCanceled ();
			serverReadyTask.TrySetCanceled ();
		}

		protected virtual void OnError (Exception error)
		{
			serverStartTask.TrySetException (error);
			serverReadyTask.TrySetResult (error);
		}

		async Task<bool> ReuseConnection (TestContext ctx, HttpConnection connection, CancellationToken cancellationToken)
		{
			var me = $"{ME}({connection.ME}) REUSE";
			ctx.LogDebug (2, $"{me}");

			serverStartTask.TrySetResult (null);

			cancellationToken.ThrowIfCancellationRequested ();
			var reusable = await connection.ReuseConnection (ctx, cancellationToken).ConfigureAwait (false);

			ctx.LogDebug (2, $"{me} #1: {reusable}");
			return reusable;
		}

		async Task<bool> InitConnection (TestContext ctx, HttpOperation operation,
		                                 HttpConnection connection, CancellationToken cancellationToken)
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

		public abstract Task Run (TestContext ctx, CancellationToken cancellationToken);

		public abstract void PrepareRedirect (TestContext ctx, HttpConnection connection, bool keepAlive);

		protected abstract void Close ();

		bool disposed;

		public void Dispose ()
		{
			if (disposed)
				return;
			disposed = true;
			Close ();
		}
	}
}
