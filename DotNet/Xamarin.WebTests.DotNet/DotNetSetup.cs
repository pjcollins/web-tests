﻿using System;
using System.Net;
using System.Net.Security;
using System.Reflection;
using System.Threading.Tasks;
using Xamarin.WebTests.ConnectionFramework;

namespace Xamarin.WebTests.DotNet
{
	class DotNetSetup : IConnectionFrameworkSetup
	{
		public string Name => "DotNet";

		public bool InstallDefaultCertificateValidator => true;

		public bool SupportsTls12 => true;

		public bool SupportsCleanShutdown => false;

		public bool UsingAppleTls => false;

		public bool UsingBtls => false;

		public bool HasNewWebStack => false;

		public bool UsingDotNet => true;

		public bool SupportsGZip => true;

		public int InternalVersion => 0;

		public void Initialize (ConnectionProviderFactory factory)
		{
			;
		}

		public Task ShutdownAsync (SslStream stream)
		{
			return stream.ShutdownAsync ();
		}
	}
}
