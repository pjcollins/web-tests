//
// ProxyOperation.cs
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
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Xamarin.AsyncTests;

namespace Xamarin.WebTests.Server
{
	using HttpFramework;
	using HttpHandlers;

	class ProxyOperation : ListenerOperation
	{
		public BuiltinProxyServer Server {
			get;
		}

		public ListenerOperation TargetOperation {
			get;
		}

		public ProxyOperation (Listener listener, ListenerOperation target)
			: base (listener, target.Operation, target.Handler, target.Uri)
		{
			Server = (BuiltinProxyServer)listener.Server;
			TargetOperation = target;
		}

		protected override async Task<HttpResponse> HandleRequest (
			TestContext ctx, HttpOperation operation, HttpConnection connection,
			HttpRequest request, CancellationToken cancellationToken)
		{
			await Task.Yield ();

			throw new NotImplementedException ();
		}

		internal async Task<bool> HandleConnection (
			TestContext ctx, HttpOperation operation, HttpConnection connection, HttpRequest request,
			Handler handler, CancellationToken cancellationToken)
		{
			var proxyConnection = (ProxyConnection)connection;

			var remoteAddress = connection.RemoteEndPoint.Address;
			request.AddHeader ("X-Forwarded-For", remoteAddress);

			ctx.LogDebug (5, $"{ME} HANDLE CONNECTION: {remoteAddress}");

			var targetOperation = proxyConnection.TargetListener.RegisterOperation (ctx, operation, handler, request.Path);

			if (Server.AuthenticationManager != null) {
				AuthenticationState state;
				var response = Server.AuthenticationManager.HandleAuthentication (ctx, connection, request, out state);
				if (response != null) {
					await connection.WriteResponse (ctx, response, cancellationToken);
					return false;
				}

				// HACK: Mono rewrites chunked requests into non-chunked.
				request.AddHeader ("X-Mono-Redirected", "true");
			}

			var serverTask = targetOperation.ServerFinishedTask;
			var proxyTask = HandleConnection (ctx, operation, proxyConnection, request, cancellationToken);

			bool serverFinished = false, proxyFinished = false;
			while (!serverFinished || !proxyFinished) {
				ctx.LogDebug (5, $"{ME} HANDLE CONNECTION #1: {serverFinished} {proxyFinished}");

				var list = new List<Task> ();
				if (!serverFinished)
					list.Add (serverTask);
				if (!proxyFinished)
					list.Add (proxyTask);
				var ret = await Task.WhenAny (list).ConfigureAwait (false);
				ctx.LogDebug (5, $"{ME} HANDLE CONNECTION #2: {serverTask.Status} {proxyTask.Status}");
				if (ret.Status == TaskStatus.Canceled || ret.Status == TaskStatus.Faulted)
					throw ret.Exception;
				if (ret == serverTask)
					serverFinished = true;
				if (ret == proxyTask)
					proxyFinished = true;
			}

			return false;
		}

		async Task HandleConnection (TestContext ctx, HttpOperation operation,
					     ProxyConnection connection, HttpRequest request,
					     CancellationToken cancellationToken)
		{
			cancellationToken.ThrowIfCancellationRequested ();

			if (request.Method.Equals ("CONNECT")) {
				await CreateTunnel (ctx, connection, connection.Stream, request, cancellationToken);
				return;
			}

			using (var targetConnection = new SocketConnection (Server)) {
				var targetEndPoint = new DnsEndPoint (TargetOperation.Uri.Host, TargetOperation.Uri.Port);
				ctx.LogDebug (5, $"{ME} HANDLE CONNECTION #1: {targetEndPoint}");

				cancellationToken.ThrowIfCancellationRequested ();
				await targetConnection.ConnectAsync (ctx, targetEndPoint, cancellationToken);

				ctx.LogDebug (5, $"{ME} HANDLE CONNECTION #2");

				cancellationToken.ThrowIfCancellationRequested ();
				await targetConnection.Initialize (ctx, operation, cancellationToken);

				ctx.LogDebug (5, $"{ME} HANDLE CONNECTION #3");

				var copyResponseTask = CopyResponse (ctx, connection, targetConnection, cancellationToken);

				cancellationToken.ThrowIfCancellationRequested ();
				await targetConnection.WriteRequest (ctx, request, cancellationToken);

				ctx.LogDebug (5, $"{ME} HANDLE CONNECTION #4");

				cancellationToken.ThrowIfCancellationRequested ();
				await copyResponseTask;

				ctx.LogDebug (5, $"{ME} HANDLE CONNECTION #5");
			}
		}

		async Task CopyResponse (TestContext ctx, HttpConnection connection,
					 HttpConnection targetConnection, CancellationToken cancellationToken)
		{
			await Task.Yield ();

			cancellationToken.ThrowIfCancellationRequested ();
			var response = await targetConnection.ReadResponse (ctx, cancellationToken).ConfigureAwait (false);
			response.SetHeader ("Connection", "close");
			response.SetHeader ("Proxy-Connection", "close");

			cancellationToken.ThrowIfCancellationRequested ();
			await connection.WriteResponse (ctx, response, cancellationToken);
		}

		static IPEndPoint GetConnectEndpoint (HttpRequest request)
		{
			var pos = request.Path.IndexOf (':');
			if (pos < 0)
				return new IPEndPoint (IPAddress.Parse (request.Path), 443);

			var address = IPAddress.Parse (request.Path.Substring (0, pos));
			var port = int.Parse (request.Path.Substring (pos + 1));
			return new IPEndPoint (address, port);
		}

		async Task CreateTunnel (
			TestContext ctx, HttpConnection connection, Stream stream,
			HttpRequest request, CancellationToken cancellationToken)
		{
			var targetEndpoint = GetConnectEndpoint (request);
			var targetSocket = new Socket (AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
			targetSocket.Connect (targetEndpoint);
			targetSocket.NoDelay = true;

			var targetStream = new NetworkStream (targetSocket, true);

			var connectionEstablished = new HttpResponse (HttpStatusCode.OK, HttpProtocol.Http10, "Connection established");
			await connectionEstablished.Write (ctx, stream, cancellationToken).ConfigureAwait (false);

			try {
				await RunTunnel (ctx, stream, targetStream, cancellationToken);
			} catch (OperationCanceledException) {
				throw;
			} catch (Exception ex) {
				System.Diagnostics.Debug.WriteLine ("ERROR: {0}", ex);
				cancellationToken.ThrowIfCancellationRequested ();
				throw;
			} finally {
				targetSocket.Dispose ();
			}
		}

		async Task RunTunnel (TestContext ctx, Stream input, Stream output, CancellationToken cancellationToken)
		{
			await Task.Yield ();

			bool doneSending = false;
			bool doneReading = false;
			Task<bool> inputTask = null;
			Task<bool> outputTask = null;

			while (!doneReading && !doneSending) {
				cancellationToken.ThrowIfCancellationRequested ();

				ctx.LogDebug (5, "RUN TUNNEL: {0} {1} {2} {3}",
					      doneReading, doneSending, inputTask != null, outputTask != null);

				if (!doneReading && inputTask == null)
					inputTask = Copy (ctx, input, output, cancellationToken);
				if (!doneSending && outputTask == null)
					outputTask = Copy (ctx, output, input, cancellationToken);

				var tasks = new List<Task<bool>> ();
				if (inputTask != null)
					tasks.Add (inputTask);
				if (outputTask != null)
					tasks.Add (outputTask);

				ctx.LogDebug (5, "RUN TUNNEL #1: {0}", tasks.Count);
				var result = await Task.WhenAny (tasks).ConfigureAwait (false);
				ctx.LogDebug (5, "RUN TUNNEL #2: {0} {1} {2}", result, result == inputTask, result == outputTask);

				if (result.IsCanceled) {
					ctx.LogDebug (5, "RUN TUNNEL - CANCEL");
					throw new TaskCanceledException ();
				}
				if (result.IsFaulted) {
					ctx.LogDebug (5, "RUN TUNNEL - ERROR: {0}", result.Exception);
					throw result.Exception;
				}

				ctx.LogDebug (5, "RUN TUNNEL #3: {0}", result.Result);

				if (result == inputTask) {
					if (!result.Result)
						doneReading = true;
					inputTask = null;
				} else if (result == outputTask) {
					if (!result.Result)
						doneSending = true;
					outputTask = null;
				} else {
					throw new NotSupportedException ();
				}
			}
		}

		async Task<bool> Copy (TestContext ctx, Stream input, Stream output, CancellationToken cancellationToken)
		{
			var buffer = new byte[4096];
			int ret;
			try {
				ret = await input.ReadAsync (buffer, 0, buffer.Length, cancellationToken).ConfigureAwait (false);
			} catch {
				cancellationToken.ThrowIfCancellationRequested ();
				throw;
			}
			if (ret == 0) {
				try {
					output.Dispose ();
				} catch {
					;
				}
				return false;
			}

			try {
				await output.WriteAsync (buffer, 0, ret, cancellationToken);
			} catch {
				cancellationToken.ThrowIfCancellationRequested ();
				throw;
			}
			return true;
		}
	}
}
