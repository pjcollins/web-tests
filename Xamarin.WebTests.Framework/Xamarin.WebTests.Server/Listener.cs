﻿//
// Listener.cs
//
// Author:
//       Martin Baulig <martin.baulig@xamarin.com>
//
// Copyright (c) 2014 Xamarin Inc. (http://www.xamarin.com)
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
	using TestFramework;

	abstract class Listener : IDisposable
	{
		LinkedList<ListenerContext> connections;
		Dictionary<string, HttpOperation> registry;
		volatile bool disposed;

		static int nextID;
		static long nextRequestID;

		public readonly int ID = ++nextID;

		internal TestContext TestContext {
			get;
		}

		internal ListenerBackend Backend {
			get;
		}

		internal HttpServer Server {
			get;
		}

		internal string ME {
			get;
		}

		public Listener (TestContext ctx, HttpServer server, ListenerBackend backend)
		{
			TestContext = ctx;
			Server = server;
			Backend = backend;
			ME = $"{GetType ().Name}({ID})";
			connections = new LinkedList<ListenerContext> ();
			registry = new Dictionary<string, HttpOperation> ();
		}

		protected internal string FormatConnection (HttpConnection connection)
		{
			return $"[{ME}:{connection.ME}]";
		}

		protected void Debug (string message)
		{
			TestContext.LogDebug (5, $"{ME}: {message}");
		}

		public Uri RegisterOperation (TestContext ctx, HttpOperation operation)
		{
			lock (this) {
				var id = Interlocked.Increment (ref nextRequestID);
				var path = $"/id/{operation.ID}/{operation.Handler.GetType ().Name}/";
				var uri = new Uri (Server.TargetUri, path);
				registry.Add (path, operation);
				return uri;
			}
		}

		protected bool GetOperation (ListenerContext context, Task<HttpRequest> task)
		{
			lock (this) {
				var me = $"{nameof (GetOperation)}({context.Connection.ME})";
				if (task.Status == TaskStatus.Canceled || task.Status == TaskStatus.Faulted) {
					Debug ($"{me} FAILED: {task.Status} {task.Exception?.Message}");
					return false;
				}

				var request = task.Result;
				Debug ($"{me} {request.Method} {request.Path} {request.Protocol}");

				var operation = registry[request.Path];
				if (operation == null) {
					Debug ($"{me} INVALID PATH: {request.Path}!");
					return false;
				}

				registry.Remove (request.Path);
				context.StartOperation (operation, request);
				return true;
			}
		}

		void CloseAll ()
		{
			lock (this) {
				TestContext.LogDebug (5, $"{ME}: CLOSE ALL");

				var iter = connections.First;
				while (iter != null) {
					var node = iter.Value;
					iter = iter.Next;

					node.Dispose ();
					connections.Remove (node);
				}
			}
		}

		protected (ListenerContext context, bool reused) FindOrCreateContext (HttpOperation operation, bool reuse)
		{
			lock (this) {
				var iter = connections.First;
				while (reuse && iter != null) {
					var node = iter.Value;
					iter = iter.Next;

					if (node.StartOperation (operation))
						return (node, true);
				}

				var context = new InstrumentationListenerContext (this);
				context.StartOperation (operation);
				connections.AddLast (context);
				return (context, false);
			}
		}

		internal void Continue (TestContext ctx, ListenerContext context, bool keepAlive)
		{
			lock (this) {
				ctx.LogDebug (5, $"{ME} CONTINUE: {keepAlive}");
				if (keepAlive) {
					context.Continue ();
					return;
				}
				connections.Remove (context);
				context.Dispose ();
			}
		}

		public ListenerContext CreateContext (TestContext ctx, HttpOperation operation, bool reusing)
		{
			var (context, _) = FindOrCreateContext (operation, reusing);
			return context;
		}

		public async Task<ListenerContext> CreateContext (
			TestContext ctx, HttpOperation operation, CancellationToken cancellationToken)
		{
			var reusing = !operation.HasAnyFlags (HttpOperationFlags.DontReuseConnection);
			var (context, reused) = FindOrCreateContext (operation, reusing);

			if (reused && operation.HasAnyFlags (HttpOperationFlags.ClientUsesNewConnection)) {
				try {
					await context.Connection.ReadRequest (ctx, cancellationToken).ConfigureAwait (false);
					throw ctx.AssertFail ("Expected client to use a new connection.");
				} catch (OperationCanceledException) {
					throw;
				} catch (Exception ex) {
					ctx.LogDebug (2, $"{ME} EXPECTED EXCEPTION: {ex.GetType ()} {ex.Message}");
				}
				context.Dispose ();
				(context, reused) = FindOrCreateContext (operation, false);
			}

			return context;
		}

		protected abstract void Close ();

		public void Dispose ()
		{
			lock (this) {
				if (disposed)
					return;
				disposed = true;
				CloseAll ();
				Close ();
				Backend.Dispose ();
			}
		}

	}
}
