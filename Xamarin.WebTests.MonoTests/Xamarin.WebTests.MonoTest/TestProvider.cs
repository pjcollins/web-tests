﻿//
// TestProvider.cs
//
// Author:
//       Martin Baulig <martin.baulig@xamarin.com>
//
// Copyright (c) 2016 Xamarin, Inc.
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
using Xamarin.AsyncTests.Constraints;
using Xamarin.WebTests.ConnectionFramework;
using Xamarin.WebTests.MonoConnectionFramework;
using Mono.Security.Interface;

namespace Xamarin.WebTests.MonoTests
{
	[Global]
	[AsyncTestFixture]
	public class TestProvider
	{
		[AsyncTest]
		public void TestDefaultProvider (TestContext ctx)
		{
			ctx.LogMessage ("TEST DEFAULT PROVIDER!");
			var setup = DependencyInjector.Get<IMonoConnectionFrameworkSetup> ();
			var provider = setup.GetDefaultProvider ();
			ctx.LogMessage ("TEST DEFAULT PROVIDER #1: {0}", provider);

			ctx.LogMessage ("SETUP: {0} - {1} - {2}", setup.Name, setup.TlsProviderName, setup.TlsProvider);

			ctx.LogMessage ("TEST: {0}", ctx.GetUniqueId ());

			ctx.Assert (provider.ID, Is.EqualTo (setup.TlsProvider), "Default TLS Provider");
		}
	}
}
