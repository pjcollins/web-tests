﻿//
// CertificateData.cs
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
using System.Security.Cryptography.X509Certificates;
using Xamarin.AsyncTests;

namespace Xamarin.WebTests.Resources
{
	using ConnectionFramework;

	public abstract class CertificateData
	{
		protected CertificateData (string name)
		{
			Name = name;
		}

		public string Name {
			get;
			private set;
		}

		public abstract byte[] Data {
			get;
		}

		public abstract X509Certificate Certificate {
			get;
		}

		public abstract bool GetCertificate (CertificateResourceType type, out X509Certificate certificate);

		public abstract bool GetCertificateWithKey (CertificateResourceType type, out X509Certificate certificate);

		public abstract bool GetCertificateData (CertificateResourceType type, out byte[] data);


		/*
		 * 			wildcardServerCertData = ResourceManager.ReadResource ("CA.wildcard-server.pfx");
			wildcardServerCert = provider.GetCertificateWithKey (wildcardServerCertData, "monkey");
			wildcardServerCertNoKeyData = ResourceManager.ReadResource ("CA.wildcard-server.pem");
			wildcardServerCertNoKey = provider.GetCertificateFromData (wildcardServerCertNoKeyData);
			wildcardServerCertBareData = ResourceManager.ReadResource ("CA.wildcard-server-bare.pfx");
			wildcardServerCertBare = provider.GetCertificateWithKey (wildcardServerCertBareData, "monkey");
			wildcardServerCertFullData = ResourceManager.ReadResource ("CA.wildcard-server-full.pfx");
			wildcardServerCertFull = provider.GetCertificateWithKey (wildcardServerCertFullData, "monkey");
*/

	}
}
