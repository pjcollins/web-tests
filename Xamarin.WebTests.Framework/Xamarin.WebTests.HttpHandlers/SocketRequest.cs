﻿//
// SocketRequest.cs
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
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using Xamarin.AsyncTests;

namespace Xamarin.WebTests.HttpHandlers
{
	public sealed class SocketRequest : Request
	{
		public Uri Uri {
			get;
		}

		public override string Method {
			get; set;
		}

		public SocketRequest (Uri uri, string method)
		{
			Uri = uri;
			Method = method;
		}

		public override Task<Response> SendAsync (TestContext ctx, CancellationToken cancellationToken)
		{
			throw new NotImplementedException ();
		}

		public override void SendChunked ()
		{
			throw new NotImplementedException ();
		}

		public override void SetContentLength (long contentLength)
		{
			throw new NotImplementedException ();
		}

		public override void SetContentType (string contentType)
		{
			throw new NotImplementedException ();
		}

		public override void SetCredentials (ICredentials credentials)
		{
			throw new NotImplementedException ();
		}

		public override void SetProxy (IWebProxy proxy)
		{
			throw new NotImplementedException ();
		}
	}
}
