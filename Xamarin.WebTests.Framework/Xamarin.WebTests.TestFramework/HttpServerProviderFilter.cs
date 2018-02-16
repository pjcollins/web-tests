//
// HttpServerProviderFilter.cs
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
using Xamarin.AsyncTests.Constraints;

namespace Xamarin.WebTests.TestFramework
{
	using ConnectionFramework;

	public class HttpServerProviderFilter
	{
		public HttpServerTestCategory Category {
			get;
		}

		public HttpServerProviderFilter (HttpServerTestCategory category)
		{
			Category = category;
		}

		bool SupportsSsl (ConnectionProvider provider)
		{
			if (!provider.SupportsSslStreams)
				return false;
			if (!provider.HasFlag (ConnectionProviderFlags.SupportsTls12))
				return false;
			return true;
		}

		bool SupportsHttpListener (ConnectionProvider provider)
		{
			return provider.HasFlag (ConnectionProviderFlags.SupportsHttpListener);
		}

		bool HasNewWebStack ()
		{
			var setup = DependencyInjector.Get<IConnectionFrameworkSetup> ();
			return setup.UsingDotNet || setup.HasNewWebStack;
		}

		bool IsSupported (TestContext ctx, ConnectionProvider provider)
		{
			if (!provider.HasFlag (ConnectionProviderFlags.SupportsHttp))
				return false;
			switch (Category) {
			case HttpServerTestCategory.Default:
			case HttpServerTestCategory.Stress:
				return SupportsSsl (provider);
			case HttpServerTestCategory.NoSsl:
				return true;
			case HttpServerTestCategory.NewWebStack:
			case HttpServerTestCategory.Experimental:
				return HasNewWebStack () && SupportsSsl (provider);
			case HttpServerTestCategory.NewWebStackNoSsl:
				return HasNewWebStack ();
			case HttpServerTestCategory.HttpListener:
				return SupportsHttpListener (provider);
			case HttpServerTestCategory.MartinTest:
				return SupportsSsl (provider);
			default:
				throw ctx.AssertFail (Category);
			}
		}

		public IEnumerable<ConnectionProvider> GetSupportedProviders (TestContext ctx)
		{
			var factory = DependencyInjector.Get<ConnectionProviderFactory> ();
			var providers = factory.GetProviders (p => IsSupported (ctx, p)).ToList ();
			if (providers.Count == 0)
				throw ctx.AssertFail ($"No supported ConnectionProvider for `{Category}'.");
			return providers;
		}
	}
}

