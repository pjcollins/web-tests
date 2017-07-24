//
// ParallelListener.cs
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
	using HttpHandlers;

	class ParallelListener : Listener
	{
		LinkedList<ParallelListenerContext> connections;
		bool closed;

		int running;
		CancellationTokenSource cts;
		AsyncManualResetEvent mainLoopEvent;

		int requestParallelConnections;

		internal int RequestParallelConnections {
			get { return requestParallelConnections; }
			set {
				lock (this) {
					if (requestParallelConnections == value)
						return;
					requestParallelConnections = value;
					Run ();
				}
			}
		}

		public ParallelListener (TestContext ctx, HttpServer server, ListenerBackend backend)
			: base (ctx, server, backend)
		{
			connections = new LinkedList<ParallelListenerContext> ();
			mainLoopEvent = new AsyncManualResetEvent (false);
			cts = new CancellationTokenSource ();

			ctx.LogDebug (5, $"{ME} TEST: {ctx.Result}");
		}

		public void Run ()
		{
			lock (this) {
				if (Interlocked.CompareExchange (ref running, 1, 0) == 0)
					MainLoop ();

				mainLoopEvent.Set ();
			}
		}

		async void MainLoop ()
		{
			while (!closed) {
				Debug ($"MAIN LOOP");

				var taskList = new List<Task> ();
				var connectionArray = new List<ParallelListenerContext> ();
				lock (this) {
					RunScheduler ();

					taskList.Add (mainLoopEvent.WaitAsync ());
					foreach (var context in connections) {
						Task task = null;
						Debug ($"  MAIN LOOP #0: {context.ME} {context.State}");
						try {
							task = context.MainLoopIteration (TestContext, cts.Token);
						} catch (Exception ex) {
							task = FailedTask (ex);
						}
						if (task != null) {
							connectionArray.Add (context);
							taskList.Add (task);
						}
					}

					Debug ($"MAIN LOOP #0: {connectionArray.Count} {taskList.Count}");
				}

				var finished = await Task.WhenAny (taskList).ConfigureAwait (false);
				Debug ($"MAIN LOOP #1: {finished.Status} {finished == taskList[0]} {taskList[0].Status}");

				lock (this) {
					if (closed)
						break;
					if (finished == taskList[0]) {
						mainLoopEvent.Reset ();
						continue;
					}

					int idx = -1;
					for (int i = 0; i < connectionArray.Count; i++) {
						if (finished == taskList[i + 1]) {
							idx = i;
							break;
						}
					}

					var context = connectionArray[idx];
					Debug ($"MAIN LOOP #2: {idx} {context.State} {context?.Connection?.ME}");

					if (finished.Status == TaskStatus.Canceled || finished.Status == TaskStatus.Faulted) {
						Debug ($"MAIN LOOP #2 FAILED: {finished.Status} {finished.Exception?.Message}");
						connections.Remove (context);
						context.Dispose ();
						continue;
					}

					if (!context.MainLoopIterationDone (TestContext, finished, cts.Token)) {
						connections.Remove (context);
						context.Dispose ();
						continue;
					}
				}
			}

			Debug ($"MAIN LOOP COMPLETE");
			cts.Dispose ();

			void RunScheduler ()
			{
				while (connections.Count < requestParallelConnections) {
					Debug ($"RUN SCHEDULER: {connections.Count}");
					var connection = Backend.CreateConnection ();
					connections.AddLast (new ParallelListenerContext (this, connection));
					Debug ($"RUN SCHEDULER #1: {connection.ME}");
				}
			}
		}

		protected override ListenerOperation CreateOperation (HttpOperation operation, Handler handler, Uri uri)
		{
			return new ParallelListenerOperation (this, operation, handler, uri);
		}

		protected override void Close ()
		{
			Debug ($"CLOSE");
			closed = true;
			cts.Cancel ();

			var iter = connections.First;
			while (iter != null) {
				var node = iter.Value;
				iter = iter.Next;

				node.Dispose ();
				connections.Remove (node);
			}

			mainLoopEvent.Set ();
		}
	}
}
