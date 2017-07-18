//
// NewListener.cs
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
using System.IO;
using System.Net;
using System.Linq;
using System.Text;
using System.Threading;
using System.Reflection;
using System.Net.Sockets;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Net.Security;
using System.Security.Authentication;
using System.Security.Cryptography.X509Certificates;
using SD = System.Diagnostics;

using Xamarin.AsyncTests;
using Xamarin.AsyncTests.Portable;

namespace Xamarin.WebTests.Server
{
	using ConnectionFramework;
	using HttpFramework;
	using TestFramework;

	abstract class NewListener : IDisposable
	{
		LinkedList<NewListenerContext> connections;
		volatile bool disposed;
		volatile bool closed;

		static int nextID;
		public readonly int ID = ++nextID;

		internal TestContext TestContext {
			get;
		}

		internal HttpServer Server {
			get;
		}

		internal string ME {
			get;
		}

		public NewListener (TestContext ctx, HttpServer server)
		{
			TestContext = ctx;
			Server = server;
			ME = $"{GetType ().Name}({ID})";
			connections = new LinkedList<NewListenerContext> ();
			mainLoopEvent = new AsyncManualResetEvent (false);
			cts = new CancellationTokenSource ();
		}

		int running;
		CancellationTokenSource cts;
		AsyncManualResetEvent mainLoopEvent;

		public void Run ()
		{
			lock (this) {
				if (Interlocked.CompareExchange (ref running, 1, 0) == 0)
					MainLoop ();

				mainLoopEvent.Set ();
			}
		}

		internal void Debug (string message)
		{
			TestContext.LogDebug (5, $"{ME}: {message}");
		}

		async void MainLoop ()
		{
			while (true) {
				Debug ($"MAIN LOOP");

				var taskList = new List<Task> ();
				lock (this) {
					taskList.Add (mainLoopEvent.WaitAsync ());
					foreach (var connection in connections)
						taskList.Add (connection.Run (cts.Token));
				}

				var ret = await Task.WhenAny (taskList).ConfigureAwait (false);
				Debug ($"MAIN LOOP #1: {ret} {ret == taskList[0]}");

				lock (this) {
					mainLoopEvent.Reset ();
				}
			}
		}

		public void Initialize (int numConnections)
		{
			lock (this) {
				for (int i = 0; i < numConnections; i++) {
					var connection = CreateConnection ();
					connections.AddLast (connection);
				}
				Run ();
			}
		}

		public void AddConnection ()
		{
			lock (this) {
				var connection = CreateConnection ();
				connections.AddLast (connection);
				Run ();
			}
		}

		public virtual void CloseAll ()
		{
			lock (this) {
				if (closed)
					return;
				closed = true;
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

		protected virtual void Shutdown ()
		{
		}

		protected virtual void OnStop ()
		{
			cts.Cancel ();
		}

		protected abstract NewListenerContext CreateConnection ();

		public abstract Task<HttpConnection> AcceptAsync (CancellationToken cancellationToken);

		public void Dispose ()
		{
			lock (this) {
				if (disposed)
					return;
				disposed = true;
				CloseAll ();
				Shutdown ();
				cts.Dispose ();
			}
		}
	}
}
