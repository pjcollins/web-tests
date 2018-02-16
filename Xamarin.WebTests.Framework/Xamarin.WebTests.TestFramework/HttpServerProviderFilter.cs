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
		public HttpServerProviderFlags Flags {
			get;
		}

		public HttpServerProviderFilter (HttpServerProviderFlags flags)
		{
			Flags = flags;
		}

		static (bool match, bool success, bool wildcard) MatchesFilter (ConnectionProvider provider, string filter)
		{
			if (filter == null)
				return (false, false, false);

			var parts = filter.Split (',');
			foreach (var part in parts) {
				if (part.Equals ("*"))
					return (true, true, true);
				if (string.Equals (provider.Name, part, StringComparison.OrdinalIgnoreCase))
					return (true, true, false);
			}

			return (true, false, false);
		}

		bool HasFlag (HttpServerProviderFlags flag)
		{
			return (Flags & flag) == flag;
		}

		bool IsSupported (ConnectionProvider provider)
		{
			if (HasFlag (HttpServerProviderFlags.AssumeSupportedByTest))
				return true;
			if (!provider.HasFlag (ConnectionProviderFlags.SupportsHttp))
				return false;
			if (HasFlag (HttpServerProviderFlags.RequireSsl)) {
				if (!provider.HasFlag (ConnectionProviderFlags.SupportsSslStream) ||
				    !provider.HasFlag (ConnectionProviderFlags.SupportsTls12))
					return false;
			}
			return true;
		}

		bool IsSupported (ConnectionProvider provider, string filter)
		{
			if (!IsSupported (provider))
				return false;

			var (match, success, wildcard) = MatchesFilter (provider, filter);
			if (match) {
				if (!success)
					return false;
				if (wildcard && !HasFlag (HttpServerProviderFlags.AllowWildcardMatches) && provider.HasFlag (ConnectionProviderFlags.IsExplicit))
					return false;
				return true;
			}

			if (provider.HasFlag (ConnectionProviderFlags.IsExplicit))
				return false;

			return true;
		}

		public IEnumerable<ConnectionProvider> GetSupportedProviders (TestContext ctx, string filter)
		{
			var factory = DependencyInjector.Get<ConnectionProviderFactory> ();
			var providers = factory.GetProviders (p => IsSupported (p)).ToList ();
			if (providers.Count == 0)
				return new ConnectionProvider[0];

			var filteredProviders = providers.Where (p => IsSupported (p, filter));

			if (filter != null && filteredProviders.Count ()  == 0)
				ctx.LogMessage ($"WARNING: No TLS Provider matches server filter '{filter}'");

			return filteredProviders;
		}
	}
}

