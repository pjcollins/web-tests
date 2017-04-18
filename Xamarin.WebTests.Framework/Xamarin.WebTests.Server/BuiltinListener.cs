﻿//
// BuiltinListener.cs
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
	using HttpFramework;

	abstract class BuiltinListener : Listener
	{
		Socket server;
		int currentConnections;
		volatile Exception currentError;
		volatile TaskCompletionSource<bool> tcs;
		volatile CancellationTokenSource cts;

		public BuiltinListener (IPortableEndPoint endpoint, HttpServerFlags flags)
			: base (endpoint, flags)
		{
			server = new Socket (AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
			server.Bind (NetworkEndPoint);
			server.Listen (1);
		}

		static void Debug (string message, params object[] args)
		{
			SD.Debug.WriteLine (message, args);
		}

		public override Task Start ()
		{
			lock (this) {
				if (cts != null)
					throw new InvalidOperationException ();

				cts = new CancellationTokenSource ();
				tcs = new TaskCompletionSource<bool> ();
			}

			return Task.Run (() => {
				Listen ();
			});
		}

		void Listen ()
		{
			var args = new SocketAsyncEventArgs ();
			args.Completed += (sender, e) => OnAccepted (e);

			Interlocked.Increment (ref currentConnections);

			try {
				var retval = server.AcceptAsync (args);
				if (retval)
					return;
				throw new InvalidOperationException ();
			} catch (Exception ex) {
				OnException (ex);
				OnFinished ();
				throw;
			}
		}

		void OnException (Exception error)
		{
			lock (this) {
				if (currentError == null) {
					currentError = error;
					return;
				}

				var aggregated = currentError as AggregateException;
				if (aggregated == null) {
					currentError = new AggregateException (error);
					return;
				}

				var inner = aggregated.InnerExceptions.ToList ();
				inner.Add (error);
				currentError = new AggregateException (inner);
			}
		}

		void OnAccepted (SocketAsyncEventArgs args)
		{
			if (cts.IsCancellationRequested) {
				OnFinished ();
				args.Dispose ();
				return;
			} else if (args.SocketError != SocketError.Success) {
				var error = new IOException (string.Format ("Accept failed: {0}", args.SocketError));
				OnException (error);
				args.Dispose ();
				return;
			}

			try {
				Listen ();
			} catch {
				return;
			}

			var socket = args.AcceptSocket;
			HttpConnection connection;

			try {
				connection = CreateConnection (socket);
			} catch (OperationCanceledException) {
				connection = null;
			} catch (Exception ex) {
				if (!cts.IsCancellationRequested)
					OnException (ex);
				connection = null;
			}

			HandleConnection_internal (socket, connection, cts.Token).ContinueWith (t => {
				if (t.IsFaulted)
					OnException (t.Exception);
				if (t.IsCompleted)
					Close (socket);

				OnFinished ();
				args.Dispose ();
			});
		}

		void OnFinished ()
		{
			lock (this) {
				var connections = Interlocked.Decrement (ref currentConnections);

				if (connections > 0)
					return;

				if (currentError != null)
					tcs.SetException (currentError);
				else
					tcs.SetResult (true);
			}
		}

		public override async Task Stop ()
		{
			cts.Cancel ();
			if (server.Connected)
				server.Shutdown (SocketShutdown.Both);
			server.Close ();
			await tcs.Task;
			OnStop ();

			lock (this) {
				cts.Dispose ();
				cts = null;
				tcs = null;
			}
		}

		protected virtual void OnStop ()
		{
		}

		void Close (Socket socket)
		{
			try {
				socket.Shutdown (SocketShutdown.Both);
			} catch {
				;
			} finally {
				socket.Close ();
				socket.Dispose ();
			}
		}

		protected abstract HttpConnection CreateConnection (Socket socket);

		bool IsStillConnected (Socket socket)
		{
			try {
				if (!socket.Poll (-1, SelectMode.SelectRead))
					return false;
				return socket.Available > 0;
			} catch {
				return false;
			}
		}

		async Task HandleConnection_internal (Socket socket, HttpConnection connection, CancellationToken cancellationToken)
		{
			if (connection == null)
				return;
			while (!cancellationToken.IsCancellationRequested) {
				var wantToReuse = await HandleConnection (socket, connection, cancellationToken);
				if (!wantToReuse || cancellationToken.IsCancellationRequested)
					break;

				bool connectionAvailable = IsStillConnected (socket);
				if (!connectionAvailable && !cts.IsCancellationRequested)
					throw new InvalidOperationException ("Expecting another connection, but socket has been shut down.");
			}
		}

		protected abstract Task<bool> HandleConnection (Socket socket, HttpConnection connection, CancellationToken cancellationToken);
	}
}
