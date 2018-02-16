﻿//
// TestHttpListener.cs
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
using System.Collections.Generic;
using Xamarin.AsyncTests;

namespace Xamarin.WebTests.Tests {
	using TestAttributes;
	using TestFramework;
	using HttpFramework;
	using HttpHandlers;
	using HttpOperations;
	using TestRunners;

	[AsyncTestFixture]
	public class TestHttpListener : ITestParameterSource<HttpListenerHandler> {
		public IEnumerable<HttpListenerHandler> GetParameters (TestContext ctx, string filter)
		{
			switch (filter) {
			case "martin":
				yield return new HttpListenerHandler (HttpListenerTestType.MartinTest);
				break;
			}
		}

		[Martin ("HttpListener")]
		[ConnectionTestFlags (ConnectionTestFlags.RequireMonoServer)]
		[HttpServerFlags (HttpServerFlags.HttpListener)]
		[AsyncTest (ParameterFilter = "martin", Unstable = true)]
		public async Task MartinTest (TestContext ctx, HttpServer server, HttpListenerHandler handler,
		                              CancellationToken cancellationToken)
		{
			using (var operation = new HttpListenerOperation (server, handler)) {
				await operation.Run (ctx, cancellationToken).ConfigureAwait (false);
			}
		}
	}
}
