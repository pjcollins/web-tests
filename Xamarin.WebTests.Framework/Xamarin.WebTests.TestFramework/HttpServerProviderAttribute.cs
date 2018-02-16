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
		public HttpServerTestCategory? Category {
			get;
		}

		public HttpServerProviderAttribute ()
			: base (null, TestFlags.Browsable)
		{
		}

		public HttpServerProviderAttribute (HttpServerTestCategory category)
			: base (null, TestFlags.Browsable)
		{
			Category = category;
		}

		bool UsingSsl (HttpServerTestCategory category)
		{
			switch (category) {
			case HttpServerTestCategory.NoSsl:
			case HttpServerTestCategory.NewWebStackNoSsl:
			case HttpServerTestCategory.HttpListener:
				return false;
			default:
				return true;
			}
		}

		bool UsingHttpListener (HttpServerTestCategory category)
		{
			switch (category) {
			case HttpServerTestCategory.HttpListener:
				return true;
			default:
				return false;
			}
		}

		bool IsMartinTest (HttpServerTestCategory category)
		{
			switch (category) {
			case HttpServerTestCategory.MartinTest:
				return true;
			default:
				return false;
			}
		}

		public IEnumerable<HttpServerProvider> GetParameters (TestContext ctx, string argument)
		{
			var category = Category ?? ctx.GetParameter<HttpServerTestCategory> ();

			if (!string.IsNullOrEmpty (argument))
				throw new NotSupportedException ();

			HttpServerFlags serverFlags = HttpServerFlags.None;
			if (UsingHttpListener (category))
				serverFlags |= HttpServerFlags.HttpListener;

			if (IsMartinTest (category)) {
				yield return new HttpServerProvider ("https", serverFlags, null);
				yield break;
			}

			if (!UsingSsl (category)) {
				serverFlags |= HttpServerFlags.NoSSL;
				yield return new HttpServerProvider ("http", serverFlags, null);
				yield break;
			}

			var filter = new HttpServerProviderFilter (category);
			var supportedProviders = filter.GetSupportedProviders (ctx);
			if (supportedProviders.Count () == 0)
				ctx.AssertFail ("Could not find any supported HttpServerProvider.");

			serverFlags |= HttpServerFlags.SSL;
			foreach (var provider in supportedProviders) {
				yield return new HttpServerProvider (
					$"https:{provider.Name}", serverFlags,
					provider.SslStreamProvider);
			}
		}
	}
}
