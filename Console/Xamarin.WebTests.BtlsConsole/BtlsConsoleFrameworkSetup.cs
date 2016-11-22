﻿//
// BtlsConsoleFrameworkSetup.cs
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
using System.IO;

namespace Xamarin.WebTests.BtlsConsole
{
	using TestFramework;
	using MonoTestProvider;
	using MonoConnectionFramework;
	using ConnectionFramework;
	using Mono.Btls.TestFramework;
	using System.Globalization;

	class BtlsConsoleFrameworkSetup : MonoConnectionFrameworkSetup, ITempDirectorySupport
	{
		public override string Name {
			get { return "Xamarin.WebTests.BtlsConsole"; }
		}

		public override bool UsingAppleTls {
			get {
				return false;
			}
		}

		public override string TlsProviderName {
			get {
#if BTLS
				return "btls";
#else
				if (UsingBtls)
					return "btls";
				else
					return "legacy";
#endif
			}
		}

		public override Guid TlsProvider {
			get {
#if BTLS
				return ConnectionProviderFactory.BoringTlsGuid;
#else
				if (UsingBtls)
					return ConnectionProviderFactory.BoringTlsGuid;
				else
					return ConnectionProviderFactory.LegacyTlsGuid;
#endif
			}
		}

		public override bool SupportsTls12 {
			get {
#if BTLS
				return true;
#else
				return UsingBtls;
#endif
			}
		}

		public string CreateTempDirectory ()
		{
			var temp = Path.GetTempPath ();
			var guid = Guid.NewGuid ();
			var path = Path.Combine (temp, guid.ToString ());
			Directory.CreateDirectory (path);
			return path;
		}

		public void DeleteTempDirectory (string path)
		{
			Directory.Delete (path);
		}

		public bool FileExists (string filename)
		{
			return File.Exists (filename);
		}

		public Stream CreateFile (string filename)
		{
			return File.Create (filename);
		}

		public void DeleteFile (string filename)
		{
			File.Delete (filename);
		}
	}
}

