﻿//
// TouchLauncher.cs
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

	class TouchLauncher : ApplicationLauncher
	{
		public Program Program {
			get;
		}

		public string SdkRoot {
			get;
		}

		public string MonoTouchRoot {
			get;
		}

		public string MTouch {
			get;
		}

		public string Application {
			get;
		}

		public Command Command {
			get;
		}

		public string RedirectStdout {
			get;
		}

		public string RedirectStderr {
			get;
		}

		public string DeviceName {
			get;
		}

		public string ExtraMTouchArguments {
			get;
		}

		public string DeviceType {
			get;
		}

		public string Runtime {
			get;
		}

		Process process;
		TaskCompletionSource<bool> tcs;

		static string GetEnvironmentVariable (string name, string defaultValue)
		{
			var value = Environment.GetEnvironmentVariable (name);
			if (string.IsNullOrEmpty (value))
				value = defaultValue;
			return value;
		}

		public TouchLauncher (Program program, string app, Command command, string sdkroot, string stdout, string stderr, string extraArgs)
		{
			Program = program;
			Application = app;
			Command = command;
			RedirectStdout = stdout;
			RedirectStderr = stderr;
			ExtraMTouchArguments = extraArgs;
			SdkRoot = sdkroot;

			MonoTouchRoot = GetEnvironmentVariable ("MONOTOUCH_ROOT", "/Library/Frameworks/Xamarin.iOS.framework/Versions/Current");

			if (String.IsNullOrEmpty (SdkRoot)) {
				SdkRoot = GetEnvironmentVariable ("XCODE_DEVELOPER_ROOT", "/Applications/Xcode.app/Contents/Developer");
			}

			MTouch = Path.Combine (MonoTouchRoot, "bin", "mtouch");

			switch (command) {
			case Command.Device:
			case Command.Simulator:
				DeviceType = Program.IOSDeviceType ?? GetEnvironmentVariable ("IOS_DEVICE_TYPE", "iPhone-5s");
				Runtime = Program.IOSRuntime ?? GetEnvironmentVariable ("IOS_RUNTIME", "iOS-10-3");
				break;
			case Command.TVOS:
				DeviceType = "Apple-TV-1080p";
				Runtime = "tvOS-9-2";
				break;
			default:
				throw new NotSupportedException ();
			}

			DeviceName = string.Format (":v2;devicetype=com.apple.CoreSimulator.SimDeviceType.{0},runtime=com.apple.CoreSimulator.SimRuntime.{1}", DeviceType, Runtime);
		}

		void Install ()
		{
			var args = new StringBuilder ();
			switch (Command) {
			case Command.Device:
				args.AppendFormat (" --installdev={0}", Application);
				break;
			case Command.Simulator:
				args.AppendFormat (" --installsim={0}", Application);
				break;
			case Command.TVOS:
				args.AppendFormat (" --installsim={0}", Application);
				break;
			default:
				throw new NotSupportedException ();
			}

			args.AppendFormat (" --sdkroot={0}", SdkRoot);
			args.AppendFormat ("  --device={0}", DeviceName);

			if (ExtraMTouchArguments != null) {
				args.Append (" ");
				args.Append (ExtraMTouchArguments);
			}

			Program.Debug ("Launching mtouch: {0} {1}", MTouch, args);

			var psi = new ProcessStartInfo (MTouch, args.ToString ());
			psi.UseShellExecute = false;
			psi.RedirectStandardInput = true;

			var installProcess = Process.Start (psi);

			Program.Debug ("Started: {0}", installProcess);

			installProcess.WaitForExit ();

			Program.Debug ("Process finished: {0}", installProcess.ExitCode);
			if (installProcess.ExitCode != 0)
				throw new NotSupportedException ();
		}

		Process Launch (string launchArgs)
		{
			var args = new StringBuilder ();
			switch (Command) {
			case Command.Device:
				args.AppendFormat (" --launchdev={0}", Application);
				break;
			case Command.Simulator:
				args.AppendFormat (" --launchsim={0}", Application);
				break;
			case Command.TVOS:
				args.AppendFormat (" --launchsim={0}", Application);
				break;
			default:
				throw new NotSupportedException ();
			}

			args.AppendFormat (" --setenv=\"XAMARIN_ASYNCTESTS_OPTIONS={0}\"", launchArgs);
			if (!string.IsNullOrWhiteSpace (RedirectStdout))
				args.AppendFormat (" --stdout={0}", RedirectStdout);
			if (!string.IsNullOrWhiteSpace (RedirectStderr))
				args.AppendFormat (" --stderr={0}", RedirectStderr);
			if (!string.IsNullOrWhiteSpace (DeviceName))
				args.AppendFormat (" --devname={0}", DeviceName);
			args.AppendFormat (" --sdkroot={0}", SdkRoot);
			args.AppendFormat (" --device={0}", DeviceName);

			if (ExtraMTouchArguments != null) {
				args.Append (" ");
				args.Append (ExtraMTouchArguments);
			}

			Program.Debug ("Launching mtouch: {0} {1}", MTouch, args);

			var psi = new ProcessStartInfo (MTouch, args.ToString ());
			psi.UseShellExecute = false;
			psi.RedirectStandardInput = true;

			return Process.Start (psi);
		}

		public override void LaunchApplication (string args)
		{
			process = Launch (args);

			Program.Debug ("Started: {0}", process);
		}

		public override Task<bool> WaitForExit ()
		{
			var oldTcs = Interlocked.CompareExchange (ref tcs, new TaskCompletionSource<bool> (), null);
			if (oldTcs != null)
				return oldTcs.Task;

			ThreadPool.QueueUserWorkItem (_ => {
				try {
					process.WaitForExit ();
					tcs.TrySetResult (process.ExitCode == 0);
				} catch (Exception ex) {
					tcs.TrySetException (ex);
				}
			});

			return tcs.Task;
		}

		public override void StopApplication ()
		{
			try {
				if (!process.HasExited)
					process.Kill ();
			} catch {
				;
			}
		}
	}
}

