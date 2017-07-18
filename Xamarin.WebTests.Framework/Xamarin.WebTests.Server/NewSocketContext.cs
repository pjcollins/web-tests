//
// NewSocketContext.cs
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
using System.Net.Sockets;
using System.Net.Security;
using System.Threading;
using System.Threading.Tasks;
using Xamarin.AsyncTests;

namespace Xamarin.WebTests.Server
{
	using HttpFramework;

	sealed class NewSocketContext : NewListenerContext
	{
		public Socket ListenSocket {
			get;
		}

		new public NewSocketListener Listener {
			get { return (NewSocketListener)base.Listener; }
		}

		internal override string ME {
			get;
		}

		Socket socket;
		IPEndPoint remoteEndPoint;
		NetworkStream networkStream;
		SslStream sslStream;
		Stream stream;
		HttpStreamReader reader;
		HttpRequest request;

		public NewSocketContext (NewSocketListener listener, Socket socket)
			: base (listener)
		{
			ListenSocket = socket;
			ME = $"[{GetType ().Name}({Listener.ME}:{socket.LocalEndPoint})]";
		}

		protected override async Task<HttpRequest> Accept (TestContext ctx, CancellationToken cancellationToken)
		{
			cancellationToken.ThrowIfCancellationRequested ();
			ctx.LogDebug (5, $"{ME} ACCEPT");
			socket = await ListenSocket.AcceptAsync (cancellationToken).ConfigureAwait (false);
			ctx.LogDebug (5, $"{ME} ACCEPT #1: {socket.RemoteEndPoint}");

			remoteEndPoint = (IPEndPoint)socket.RemoteEndPoint;
			networkStream = new NetworkStream (socket, true);

			cancellationToken.ThrowIfCancellationRequested ();
			if (Server.SslStreamProvider != null) {
				sslStream = Server.SslStreamProvider.CreateSslStream (ctx, networkStream, Server.Parameters, true);

				var certificate = Server.Parameters.ServerCertificate;
				var askForCert = Server.Parameters.AskForClientCertificate || Server.Parameters.RequireClientCertificate;
				var sslProtocol = Server.SslStreamProvider.GetProtocol (Server.Parameters, true);

				await sslStream.AuthenticateAsServerAsync (certificate, askForCert, sslProtocol, false);
				stream = sslStream;
			} else {
				stream = networkStream;
			}

			reader = new HttpStreamReader (stream);
			ctx.LogDebug (5, $"{ME} ACCEPT #2");

			cancellationToken.ThrowIfCancellationRequested ();
			var header = await reader.ReadLineAsync (cancellationToken);
			var (method, protocol, path) = HttpMessage.ReadHttpHeader (header);
			ctx.LogDebug (5, $"{ME} ACCEPT #3: {method} {protocol} {path}");

			request = new HttpRequest (protocol, method, path, reader);
			return request;
		}

		protected override void Close ()
		{
			// throw new NotImplementedException ();
		}
	}
}
