﻿//
// ProxyBackend.cs
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
using System.Threading;
using System.Threading.Tasks;
using Xamarin.AsyncTests;
using Xamarin.AsyncTests.Portable;
using Xamarin.WebTests.ConnectionFramework;
using Xamarin.WebTests.HttpFramework;

namespace Xamarin.WebTests.Server {
	public sealed class ProxyBackend : HttpBackend {
		public HttpBackend Target {
			get;
		}
		public IPortableEndPoint ProxyEndPoint {
			get;
		}

		public ProxyBackend (HttpBackend target, IPortableEndPoint proxyEndPoint, ListenerFlags flags)
		{
			Target = target;
			ProxyEndPoint = proxyEndPoint;
			Flags = flags | ListenerFlags.Proxy;

			Uri = new Uri (string.Format ("http://{0}:{1}/", ProxyEndPoint.Address, ProxyEndPoint.Port));
		}

		public override ListenerFlags Flags {
			get;
		}

		public override bool UseSSL {
			get { return false; }
		}

		public override Uri Uri {
			get;
		}

		ProxyListener currentListener;

		public override async Task Start (TestContext ctx, HttpServer server, CancellationToken cancellationToken)
		{
			var listener = new ProxyListener (ctx, this, (ProxyServer)server);
			if (Interlocked.CompareExchange (ref currentListener, listener, null) != null)
				throw new InternalErrorException ();
			await Target.Start (ctx, server, cancellationToken).ConfigureAwait (false);
			await listener.Start ();
		}

		public override async Task Stop (TestContext ctx, HttpServer server, CancellationToken cancellationToken)
		{
			var listener = Interlocked.Exchange (ref currentListener, null);
			if (listener == null || listener.Context != ctx)
				throw new InternalErrorException ();
			try {
				await listener.Stop ().ConfigureAwait (false);
				await Target.Stop (ctx, server, cancellationToken);
			} catch {
				if ((Flags & ListenerFlags.ExpectException) == 0)
					throw;
			}
		}

		public override HttpConnection CreateConnection (TestContext ctx, HttpServer server, Stream stream)
		{
			return Target.CreateConnection (ctx, server, stream);
		}
	}
}
