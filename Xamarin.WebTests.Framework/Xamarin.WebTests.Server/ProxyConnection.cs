﻿//
// ProxyConnection.cs
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
using System.Threading;
using System.Threading.Tasks;
using Xamarin.AsyncTests;

namespace Xamarin.WebTests.Server
{
	using HttpFramework;

	class ProxyConnection : SocketConnection
	{
		public ParallelListener TargetListener {
			get;
		}

		ListenerContext targetContext;

		public ProxyConnection (BuiltinProxyServer server, Socket socket, HttpServer target)
			: base (server, socket)
		{
			TargetListener = (ParallelListener)target.Listener;
		}

		public override async Task AcceptAsync (TestContext ctx, CancellationToken cancellationToken)
		{
			await base.AcceptAsync (ctx, cancellationToken).ConfigureAwait (false);
		}

		public override Task Initialize (TestContext ctx, HttpOperation operation, CancellationToken cancellationToken)
		{
			targetContext = TargetListener.CreateContext (ctx, operation, false);
			return base.Initialize (ctx, operation, cancellationToken);
		}

		internal async Task RunTarget (TestContext ctx, HttpOperation operation, CancellationToken cancellationToken)
		{
			await targetContext.Run (ctx, cancellationToken).ConfigureAwait (false);
		}

		protected override void Close ()
		{
			if (targetContext != null) {
				targetContext.Dispose ();
				targetContext = null;
			}
			base.Close ();
		}
	}
}
