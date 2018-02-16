//
// HttpServerProviderAttribute.cs
//
// Author:
//       Martin Baulig <mabaul@microsoft.com>
//
// Copyright (c) 2018 Xamarin Inc. (http://www.xamarin.com)
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
using System.Linq;
using System.Collections.Generic;
using Xamarin.AsyncTests;
using Xamarin.AsyncTests.Framework;
using Xamarin.AsyncTests.Portable;

namespace Xamarin.WebTests.TestFramework
{
	using ConnectionFramework;
	using HttpFramework;
	using TestRunners;

	[AttributeUsage (AttributeTargets.Class | AttributeTargets.Parameter, AllowMultiple = false)]
	public class HttpServerProviderAttribute : TestParameterAttribute, ITestParameterSource<HttpServerProvider>
	{
		public HttpServerProviderFlags ProviderFlags {
			get;
		}

		public HttpServerProviderAttribute (HttpServerProviderFlags flags = HttpServerProviderFlags.None)
			: base (null, TestFlags.Browsable)
		{
			ProviderFlags = flags;
			Optional = true;
		}

		public bool Optional {
			get; set;
		}

		public IEnumerable<HttpServerProvider> GetParameters (TestContext ctx, string argument)
		{
			var category = ctx.GetParameter<ConnectionTestCategory> ();

			var flags = ProviderFlags;
			if (ctx.TryGetParameter (out HttpServerProviderFlags explicitFlags))
				flags |= explicitFlags;

			var filter = new HttpServerProviderFilter (flags);
			var supportedProviders = filter.GetSupportedProviders (ctx, argument);
			if (!Optional && supportedProviders.Count () == 0)
				ctx.AssertFail ("Could not find any supported HttpServerProvider.");

			yield return CreateDefault ();

			if ((flags & HttpServerProviderFlags.NoSsl) != 0)
				yield break;

			foreach (var provider in supportedProviders) {
				if (provider.SupportsSslStreams)
					yield return CreateSsl (provider);
			}

			HttpServerProvider CreateSsl (ConnectionProvider provider)
			{
				var endPoint = ConnectionTestHelper.GetEndPoint (ctx);
				var uri = new Uri ($"https://{endPoint.Address}:{endPoint.Port}/");
				return new HttpServerProvider (
					$"https:{provider.Name}", uri, endPoint,
					HttpServerFlags.SSL, provider.SslStreamProvider);
			}

			HttpServerProvider CreateDefault ()
			{
				var endPoint = ConnectionTestHelper.GetEndPoint (ctx);
				var uri = new Uri ($"http://{endPoint.Address}:{endPoint.Port}/");
				return new HttpServerProvider (
					$"http", uri, endPoint, HttpServerFlags.NoSSL, null);
			}
		}
	}
}
