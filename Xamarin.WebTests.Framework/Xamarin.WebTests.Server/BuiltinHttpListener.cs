﻿//
// BuiltinHttpListener.cs
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
using System.Text;
using System.Net;
using System.Net.Sockets;
using System.Net.Security;
using System.Collections.Generic;
using System.Security.Cryptography.X509Certificates;
using System.Threading;
using System.Threading.Tasks;

using Xamarin.AsyncTests;
using Xamarin.AsyncTests.Portable;

namespace Xamarin.WebTests.Server
{
	using HttpHandlers;
	using HttpFramework;

	class BuiltinHttpListener : BuiltinListener
	{
		public BuiltinHttpServer Server {
			get;
		}

		internal TestContext Context {
			get;
		}

		public BuiltinHttpListener (TestContext ctx, BuiltinHttpServer server)
			: base (server.ListenAddress, server.Flags)
		{
			Context = ctx;

			Server = server;
		}

		protected override HttpConnection CreateConnection (Socket socket)
		{
			var stream = new NetworkStream (socket);
			return Server.CreateConnection (Context, stream);
		}

		protected override bool HandleConnection (Socket socket, HttpConnection connection, CancellationToken cancellationToken)
		{
			var request = connection.ReadRequest ();
			return Server.HandleConnection (Context, connection, request);
		}
	}
}

