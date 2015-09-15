﻿//
// ConnectionTestRunner.cs
//
// Author:
//       Martin Baulig <martin.baulig@xamarin.com>
//
// Copyright (c) 2015 Xamarin, Inc.
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
using System.Collections.Generic;
using Xamarin.AsyncTests;

namespace Xamarin.WebTests.TestRunners
{
	using ConnectionFramework;
	using TestFramework;
	using Providers;
	using Resources;
	using Portable;

	public abstract class ConnectionTestRunner : ClientAndServer
	{
		new public ConnectionTestParameters Parameters {
			get { return (ConnectionTestParameters)base.Parameters; }
		}

		public ConnectionTestCategory Category {
			get { return Parameters.Category; }
		}

		public ConnectionTestProvider Provider {
			get;
			private set;
		}

		public ConnectionTestRunner (IServer server, IClient client, ConnectionTestProvider provider, ConnectionTestParameters parameters)
			: base (server, client, parameters)
		{
			Provider = provider;
		}

		public static IEnumerable<ConnectionTestType> GetConnectionTestTypes (TestContext ctx, ConnectionTestCategory category)
		{
			switch (category) {
			case ConnectionTestCategory.Https:
				yield return ConnectionTestType.Default;
				yield return ConnectionTestType.AcceptFromLocalCA;
				yield return ConnectionTestType.NoValidator;
				yield return ConnectionTestType.RejectAll;
				yield return ConnectionTestType.RequestClientCertificate;
				yield return ConnectionTestType.RequireClientCertificate;
				yield return ConnectionTestType.RejectClientCertificate;
				yield return ConnectionTestType.UnrequestedClientCertificate;
				yield return ConnectionTestType.OptionalClientCertificate;
				yield return ConnectionTestType.RejectClientCertificate;
				yield return ConnectionTestType.MissingClientCertificate;
				yield break;

			case ConnectionTestCategory.HttpsWithMono:
				yield return ConnectionTestType.Default;
				yield return ConnectionTestType.AcceptFromLocalCA;
				yield return ConnectionTestType.RejectAll;
				yield return ConnectionTestType.UnrequestedClientCertificate;
				yield return ConnectionTestType.RejectClientCertificate;
				yield return ConnectionTestType.MissingClientCertificate;
				yield break;

			case ConnectionTestCategory.HttpsWithDotNet:
				yield return ConnectionTestType.NoValidator;
				yield return ConnectionTestType.RequestClientCertificate;
				yield return ConnectionTestType.RequireClientCertificate;
				yield return ConnectionTestType.RejectClientCertificate;
				yield return ConnectionTestType.UnrequestedClientCertificate;
				yield return ConnectionTestType.OptionalClientCertificate;
				yield return ConnectionTestType.RejectClientCertificate;
				yield return ConnectionTestType.MissingClientCertificate;
				yield break;

			case ConnectionTestCategory.SslStreamWithTls12:
				yield return ConnectionTestType.Default;
				yield return ConnectionTestType.AcceptFromLocalCA;
				yield return ConnectionTestType.RequireClientCertificate;
				yield break;

			case ConnectionTestCategory.InvalidCertificatesInTls12:
				yield return ConnectionTestType.InvalidServerCertificate;
				yield break;

			case ConnectionTestCategory.MartinTest:
				yield return ConnectionTestType.MartinTest;
				yield break;

			default:
				ctx.AssertFail ("Unsupported test category: '{0}'.", category);
				throw new NotImplementedException ();
			}
		}

		public static bool IsSupported (TestContext ctx, ConnectionTestCategory category, ConnectionProvider provider)
		{
			var isNewTls = ConnectionTestHelper.IsNewTls (provider.Type);

			var flags = provider.Flags;
			var supportsSslStream = ((flags & ConnectionProviderFlags.SupportsSslStream) != 0);

			switch (category) {
			case ConnectionTestCategory.Https:
				return supportsSslStream;
			case ConnectionTestCategory.HttpsWithMono:
				return supportsSslStream;
			case ConnectionTestCategory.HttpsWithDotNet:
				return supportsSslStream && isNewTls;
			case ConnectionTestCategory.SslStreamWithTls12:
			case ConnectionTestCategory.InvalidCertificatesInTls12:
				return supportsSslStream && isNewTls;
			case ConnectionTestCategory.MartinTest:
				return true;
			default:
				throw new InvalidOperationException ();
			}
		}
	}
}
