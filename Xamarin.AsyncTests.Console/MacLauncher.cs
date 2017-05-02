﻿//
// MacLauncher.cs
//
// Author:
//       Martin Baulig <martin.baulig@xamarin.com>
//
// Copyright (c) 2016 Xamarin Inc. (http://www.xamarin.com)
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
using System.Linq;
using System.Text;
using System.Xml;
using System.Xml.Linq;
using System.Collections.Generic;
using System.Reflection;
using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;

namespace Xamarin.AsyncTests.Console
{
	using Remoting;
	using Portable;

	class MacLauncher : ApplicationLauncher
	{
		public Program Program {
			get;
			private set;
		}

		public string Application {
			get;
			private set;
		}

		public string RedirectStdout {
			get;
			private set;
		}

		public string RedirectStderr {
			get;
			private set;
		}

		public MacLauncher (Program program, string app, string stdout, string stderr)
		{
			Program = program;
			Application = app;
			RedirectStdout = stdout;
			RedirectStderr = stderr;
		}

		public override Task<ExternalProcess> LaunchApplication (string options, CancellationToken cancellationToken)
		{
			Program.Debug ("Launching app: {0}", Application);

			var psi = new ProcessStartInfo ("/usr/bin/open");
			psi.UseShellExecute = false;
			psi.RedirectStandardInput = true;
			psi.Arguments = "-F -W -n " + Application;
			psi.EnvironmentVariables.Add ("XAMARIN_ASYNCTESTS_OPTIONS", options);

			return ProcessHelper.RunCommand (psi, cancellationToken);
		}
	}
}

