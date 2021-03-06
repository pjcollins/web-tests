﻿//
// TestStreamInstrumentation.cs
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
using System.Threading;
using System.Threading.Tasks;
using System.Collections;
using System.Collections.Generic;

using Xamarin.AsyncTests;
using Xamarin.AsyncTests.Portable;
using Xamarin.AsyncTests.Constraints;

namespace Xamarin.WebTests.Tests
{
	using ConnectionFramework;
	using TestAttributes;
	using TestFramework;
	using TestRunners;

	[AsyncTestFixture (Timeout = 5000)]
	public class TestStreamInstrumentation
	{
		[AsyncTest]
		[ProtocolVersion (ProtocolVersions.Tls12)]
		[ConnectionTestCategory (ConnectionTestCategory.SslStreamInstrumentation)]
		public async Task Run (TestContext ctx, CancellationToken cancellationToken,
		                       ConnectionTestProvider provider, StreamInstrumentationParameters parameters,
		                       StreamInstrumentationTestRunner runner)
		{
			await runner.Run (ctx, cancellationToken);
		}

		[AsyncTest]
		[ProtocolVersion (ProtocolVersions.Tls12)]
		[ConnectionTestCategory (ConnectionTestCategory.SslStreamInstrumentationMono)]
		public async Task RunMono (TestContext ctx, CancellationToken cancellationToken,
		                           ConnectionTestProvider provider, StreamInstrumentationParameters parameters,
		                           StreamInstrumentationTestRunner runner)
		{
			await runner.Run (ctx, cancellationToken);
		}

		[AsyncTest]
		[RecentlyFixed]
		[ProtocolVersion (ProtocolVersions.Tls12)]
		[ConnectionTestCategory (ConnectionTestCategory.SslStreamInstrumentationRecentlyFixed)]
		public async Task RunRecentlyFixed (TestContext ctx, CancellationToken cancellationToken,
		                                    ConnectionTestProvider provider,
		                                    StreamInstrumentationParameters parameters,
		                                    StreamInstrumentationTestRunner runner)
		{
			await runner.Run (ctx, cancellationToken);
		}

		[AsyncTest]
		[CleanShutdown]
		[ProtocolVersion (ProtocolVersions.Tls12)]
		[ConnectionTestFlags (ConnectionTestFlags.AllowWildcardMatches)]
		[ConnectionTestCategory (ConnectionTestCategory.SslStreamInstrumentationShutdown)]
		public async Task RunShutdown (TestContext ctx, CancellationToken cancellationToken,
		                               [ConnectionTestProvider ("*:*")] ConnectionTestProvider provider,
		                               StreamInstrumentationParameters parameters,
		                               StreamInstrumentationTestRunner runner)
		{
			await runner.Run (ctx, cancellationToken);
		}

		[AsyncTest]
		[CleanShutdown]
		[ProtocolVersion (ProtocolVersions.Tls12)]
		[ConnectionTestFlags (ConnectionTestFlags.AllowWildcardMatches)]
		[ConnectionTestCategory (ConnectionTestCategory.SslStreamInstrumentationServerShutdown)]
		public async Task RunServerShutdown (TestContext ctx, CancellationToken cancellationToken,
		                                     [ConnectionTestProvider ("*:*")] ConnectionTestProvider provider,
		                                     StreamInstrumentationParameters parameters,
		                                     StreamInstrumentationTestRunner runner)
		{
			await runner.Run (ctx, cancellationToken);
		}

		[AsyncTest]
		[Experimental]
		[ProtocolVersion (ProtocolVersions.Tls12)]
		[ConnectionTestCategory (ConnectionTestCategory.SslStreamInstrumentationExperimental)]
		public async Task RunExperimental (TestContext ctx, CancellationToken cancellationToken,
		                                   ConnectionTestProvider provider, StreamInstrumentationParameters parameters,
		                                   StreamInstrumentationTestRunner runner)
		{
			await runner.Run (ctx, cancellationToken);
		}

		[AsyncTest]
		[NewWebStack]
		[ProtocolVersion (ProtocolVersions.Tls12)]
		[ConnectionTestCategory (ConnectionTestCategory.SslStreamInstrumentationNewWebStack)]
		public async Task RunNewWebStack (TestContext ctx, CancellationToken cancellationToken,
						  ConnectionTestProvider provider, StreamInstrumentationParameters parameters,
						  StreamInstrumentationTestRunner runner)
		{
			await runner.Run (ctx, cancellationToken);
		}

		[DotNet]
		[AsyncTest]
		[Martin ("StreamInstrumentation")]
		[ConnectionTestFlags (ConnectionTestFlags.RequireDotNet)]
		[ConnectionTestCategory (ConnectionTestCategory.MartinTest)]
		public async Task MartinTestDotNet (TestContext ctx, CancellationToken cancellationToken,
		                                    ConnectionTestProvider provider, StreamInstrumentationParameters parameters,
		                                    StreamInstrumentationTestRunner runner)
		{
			await runner.Run (ctx, cancellationToken);
		}

		[AsyncTest]
		[Martin ("StreamInstrumentationMono")]
		[ConnectionTestFlags (ConnectionTestFlags.RequireMono)]
		[ConnectionTestCategory (ConnectionTestCategory.MartinTest)]
		public async Task MartinTest (TestContext ctx, CancellationToken cancellationToken,
		                              ConnectionTestProvider provider, StreamInstrumentationParameters parameters,
		                              StreamInstrumentationTestRunner runner)
		{
			await runner.Run (ctx, cancellationToken);
		}

		[AsyncTest]
		[Martin ("StreamInstrumentationShutdown")]
		[ConnectionTestFlags (ConnectionTestFlags.RequireMono)]
		[ConnectionTestCategory (ConnectionTestCategory.MartinTest)]
		public async Task MartinTestShutdown (TestContext ctx, CancellationToken cancellationToken,
		                                      [ConnectionTestProvider ("apple-tls:btls")] ConnectionTestProvider provider,
		                                      StreamInstrumentationParameters parameters,
		                                      StreamInstrumentationTestRunner runner)
		{
			await runner.Run (ctx, cancellationToken);
		}

	}
}
