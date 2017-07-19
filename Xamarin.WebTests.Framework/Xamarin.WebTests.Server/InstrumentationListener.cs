//
// InstrumentationListener.cs
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
	using HttpFramework;

	class InstrumentationListener : Listener
	{
		LinkedList<ListenerContext> connections;

		public InstrumentationListener (TestContext ctx, HttpServer server, ListenerBackend backend)
			: base (ctx, server, backend)
		{
			connections = new LinkedList<ListenerContext> ();
		}

		protected override void Close ()
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

		ListenerContext FindIdleConnection (TestContext ctx, HttpOperation operation)
		{
			var iter = connections.First;
			while (iter != null) {
				var node = iter.Value;
				iter = iter.Next;

				if (node.StartOperation (operation))
					return node;
			}

			return null;
		}

		ListenerContext FindConnection (HttpConnection connection)
		{
			var iter = connections.First;
			while (iter != null) {
				var node = iter.Value;
				iter = iter.Next;
				if (node.Connection == connection)
					return node;
			}

			throw new InvalidOperationException ();
		}

		void CheckConnections (TestContext ctx)
		{
			int count = 0;
			var iter = connections.First;
			while (iter != null) {
				var node = iter.Value;
				iter = iter.Next;

				if (node.Operation == null)
					++count;
			}
			ctx.LogDebug (5, $"{ME} CHECK CONNECTIONS: {connections.Count} {count}");
		}

		public (HttpConnection connection, bool reused) CreateConnection (
			TestContext ctx, HttpOperation operation, bool reuse)
		{
			lock (this) {
				ListenerContext context = null;
				CheckConnections (ctx);
				if (reuse)
					context = FindIdleConnection (ctx, operation);

				if (context != null) {
					ctx.LogDebug (5, $"{ME} REUSING CONNECTION: {context.Connection} {connections.Count}");
					return (context.Connection, true);
				}

				var connection = Backend.CreateConnection ();
				ctx.LogDebug (5, $"{ME} CREATE CONNECTION: {connection} {connections.Count}");

				context = new ListenerContext (this, connection);
				connections.AddLast (context);
				connection.ClosedEvent += (sender, e) => {
					lock (this) {
						connections.Remove (context);
					}
				};
				if (!context.StartOperation (operation))
					throw new InvalidOperationException ();
				return (connection, false);
			}
		}

		public void Continue (TestContext ctx, HttpConnection connection, bool keepAlive)
		{
			lock (this) {
				ctx.LogDebug (5, $"{ME} CONTINUE: {connection.ME} {keepAlive}");
				var context = FindConnection (connection);
				if (keepAlive) {
					context.Continue ();
					return;
				}
				connections.Remove (context);
				context.Dispose ();
			}
		}
	}
}
