﻿//
// SocketConnection.cs
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
using System.Text;
using System.Net;
using System.Net.Security;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using Xamarin.AsyncTests;
using Xamarin.AsyncTests.Portable;
using Xamarin.AsyncTests.Constraints;
using Xamarin.WebTests.ConnectionFramework;
using Xamarin.WebTests.HttpFramework;

namespace Xamarin.WebTests.Server
{
	class SocketConnection : HttpConnection
	{
		public Socket ListenSocket {
			get;
			private set;
		}

		public Socket Socket {
			get;
			private set;
		}

		public Stream Stream {
			get;
			private set;
		}

		internal object SyncRoot {
			get;
		}

		public override SslStream SslStream => sslStream;

		internal override IPEndPoint RemoteEndPoint => remoteEndPoint;

		Stream networkStream;
		SslStream sslStream;
		HttpStreamReader reader;
		IPEndPoint remoteEndPoint;
		HttpOperation currentOperation;

		public SocketConnection (SocketListener listener, HttpServer server, Socket socket)
			: base (server)
		{
			ListenSocket = socket;
			SyncRoot = listener;
		}

		public SocketConnection (NewSocketListener listener, HttpServer server, Socket socket)
			: base (server)
		{
			ListenSocket = socket;
			SyncRoot = listener.SyncRoot;
		}

		public SocketConnection (HttpServer server)
			: base (server)
		{
		}

		public override async Task AcceptAsync (TestContext ctx, CancellationToken cancellationToken)
		{
			ctx.LogDebug (5, $"{ME} ACCEPT: {ListenSocket.LocalEndPoint}");
			lock (SyncRoot) {
				if (Socket != null)
					throw new NotSupportedException ();
			}
			Socket = await ListenSocket.AcceptAsync (cancellationToken).ConfigureAwait (false);
			remoteEndPoint = (IPEndPoint)Socket.RemoteEndPoint;
			ctx.LogDebug (5, $"{ME} ACCEPT #1: {ListenSocket.LocalEndPoint} {remoteEndPoint}");
		}

		public async Task ConnectAsync (TestContext ctx, EndPoint endpoint, CancellationToken cancellationToken)
		{
			Socket = new Socket (AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
			await Socket.ConnectAsync (endpoint, cancellationToken).ConfigureAwait (false);
			remoteEndPoint = (IPEndPoint)Socket.RemoteEndPoint;
		}

		public override async Task Initialize (TestContext ctx, HttpOperation operation, CancellationToken cancellationToken)
		{
			remoteEndPoint = (IPEndPoint)Socket.RemoteEndPoint;
			// var operation = currentOperation;
			ctx.LogDebug (5, $"{ME} INITIALIZE: {ListenSocket?.LocalEndPoint} {remoteEndPoint} {operation?.ME}");
			if (operation != null)
				networkStream = operation.CreateNetworkStream (ctx, Socket, true);
			if (networkStream == null)
				networkStream = new NetworkStream (Socket, true);

			if (Server.SslStreamProvider != null) {
				sslStream = await CreateSslStream (ctx, networkStream, cancellationToken).ConfigureAwait (false);
				Stream = sslStream;
			} else {
				Stream = networkStream;
			}

			reader = new HttpStreamReader (Stream);
			ctx.LogDebug (5, $"{ME} INITIALIZE DONE: {ListenSocket?.LocalEndPoint} {remoteEndPoint}");
		}

		async Task<SslStream> CreateSslStream (TestContext ctx, Stream innerStream, CancellationToken cancellationToken)
		{
			var stream = Server.SslStreamProvider.CreateSslStream (ctx, innerStream, Server.Parameters, true);

			var certificate = Server.Parameters.ServerCertificate;
			var askForCert = Server.Parameters.AskForClientCertificate || Server.Parameters.RequireClientCertificate;
			var protocol = Server.SslStreamProvider.GetProtocol (Server.Parameters, true);

			await stream.AuthenticateAsServerAsync (certificate, askForCert, protocol, false).ConfigureAwait (false);

			return stream;
		}

		public override async Task<bool> ReuseConnection (TestContext ctx, CancellationToken cancellationToken)
		{
			cancellationToken.ThrowIfCancellationRequested ();
			var socket = Socket;
			if (!socket.Connected)
				return false;

			var reusable = await socket.PollAsync (cancellationToken).ConfigureAwait (false);
			ctx.LogDebug (5, $"{ME}: REUSE CONNECTION: {reusable} {socket.Connected} {socket.Available} {socket.Blocking} {socket.IsBound}");
			if (!reusable)
				return false;

			return reusable;
		}

		internal override bool IsStillConnected ()
		{
			try {
				if (!Socket.Poll (-1, SelectMode.SelectRead))
					return false;
				return Socket.Available > 0;
			} catch {
				return false;
			}
		}

		public override async Task<bool> HasRequest (CancellationToken cancellationToken)
		{
			return !await reader.IsEndOfStream (cancellationToken).ConfigureAwait (false);
		}

		public override async Task<HttpRequest> ReadRequest (TestContext ctx, CancellationToken cancellationToken)
		{
			ctx.LogDebug (5, $"{ME} READ REQUEST: {ListenSocket?.LocalEndPoint} {remoteEndPoint}");
			var request = await HttpRequest.Read (ctx, reader, cancellationToken).ConfigureAwait (false);
			ctx.LogDebug (5, $"{ME} READ REQUEST DONE: {ListenSocket?.LocalEndPoint} {remoteEndPoint} - {request}");
			return request;
		}

		public override Task<HttpResponse> ReadResponse (TestContext ctx, CancellationToken cancellationToken)
		{
			return HttpResponse.Read (ctx, reader, cancellationToken);
		}

		internal override async Task WriteRequest (TestContext ctx, HttpRequest request, CancellationToken cancellationToken)
		{
			using (var writer = new StreamWriter (Stream, new ASCIIEncoding (), 1024, true)) {
				writer.AutoFlush = true;
				await request.Write (ctx, writer, cancellationToken).ConfigureAwait (false);
			}
		}

		internal override Task WriteResponse (TestContext ctx, HttpResponse response, CancellationToken cancellationToken)
		{
			return response.Write (ctx, Stream, cancellationToken);
		}

		public override bool StartOperation (TestContext ctx, HttpOperation operation)
		{
			lock (SyncRoot) {
				if (Interlocked.CompareExchange (ref currentOperation, operation, null) != null)
					return false;
				ctx.LogDebug (5, $"{ME} START OPERATION: {operation.ME}");
				StartOperation_internal (ctx, operation);
				return true;
			}
		}

		protected virtual void StartOperation_internal (TestContext ctx, HttpOperation operation)
		{
			
		}

		public override void Continue (TestContext ctx, bool keepAlive)
		{
			lock (SyncRoot) {
				ctx.LogDebug (5, $"{ME} CONTINUE: {keepAlive}");
				if (!keepAlive) {
					Close ();
					return;
				}

				currentOperation = null;
				OnClosed (true);
			}
		}

		protected override void Close ()
		{
			OnClosed (false);

			if (reader != null) {
				reader.Dispose ();
				reader = null;
			}
			if (sslStream != null) {
				sslStream.Dispose ();
				sslStream = null;
			}
			if (networkStream != null) {
				try {
					networkStream.Dispose ();
				} catch {
					;
				} finally {
					networkStream = null;
				}
			}
			if (Stream != null) {
				try {
					Stream.Dispose ();
				} catch {
					;
				} finally {
					Stream = null;
				}
			}
			if (Socket != null) {
				try {
					Socket.Shutdown (SocketShutdown.Both);
				} catch {
					;
				} finally {
					Socket.Close ();
					Socket.Dispose ();
					Socket = null;
				}
			}
		}
	}
}
