﻿//
// Program.cs
//
// Author:
//       Martin Baulig <martin.baulig@xamarin.com>
//
// Copyright (c) 2014 Xamarin Inc. (http://www.xamarin.com)
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
using SD = System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using NDesk.Options;

namespace Xamarin.AsyncTests.Console
{
	using Remoting;
	using Portable;
	using Framework;

	public class Program : TestApp
	{
		public ProgramOptions Options {
			get;
		}

		public Assembly Assembly => Options.Assembly;

		public string PackageName => Options.PackageName;

		public SettingsBag Settings => Options.Settings;

		public TestLogger Logger {
			get;
		}

		public ApplicationLauncher Launcher {
			get;
		}

		public LauncherOptions LauncherOptions {
			get;
		}

		internal DroidHelper DroidHelper {
			get;
		}

		internal TextWriter JenkinsHtml {
			get;
		}

		TestSession session;
		TestResult result;
		DateTime startTime, endTime;

		public static void Run (Assembly assembly, string[] args)
		{
			SD.Debug.AutoFlush = true;
			SD.Debug.Listeners.Add (new SD.ConsoleTraceListener ());

			DependencyInjector.RegisterAssembly (typeof(PortableSupportImpl).Assembly);

			Program program = null;
			int result;
			try {
				program = new Program (assembly, args);
			} catch (Exception ex) {
				PrintException (ex);
				Environment.Exit (-1);
			}

			try {
				var task = program.Run (CancellationToken.None);
				task.Wait ();
				result = task.Result;
			} catch (Exception ex) {
				program.LogException (ex);
				result = -1;
			}
			program.Finish ();
			Environment.Exit (result);
		}

		static void PrintException (Exception ex)
		{
			var aggregate = ex as AggregateException;
			if (aggregate != null && aggregate.InnerExceptions.Count == 1) {
				PrintException (aggregate.InnerException);
				return;
			}

			var toolEx = ex as ExternalToolException;
			if (toolEx != null) {
				if (!string.IsNullOrEmpty (toolEx.ErrorOutput))
					PrintError ("ERROR: External tool '{0}' failed:\n{1}\n", toolEx.Tool, toolEx.ErrorOutput);
				else
					PrintError ("ERROR: External tool '{0}' failed:\n{1}\n", toolEx.Tool, toolEx);
				return;
			}

			PrintError ("ERROR: {0}", ex);
		}

		static void Main (string[] args)
		{
			Run (null, args);
		}

		Program (Assembly assembly, string[] args)
		{
			Options = new ProgramOptions (assembly, args);

			switch (Options.Command) {
			case Command.Simulator:
			case Command.Device:
			case Command.TVOS:
				Launcher = new TouchLauncher (this);
				break;
			case Command.Mac:
				Launcher = new MacLauncher (this);
				break;
			case Command.Android:
				DroidHelper = new DroidHelper (this);
				Launcher = new DroidLauncher (this);
				break;
			case Command.Avd:
			case Command.Emulator:
			case Command.Apk:
				DroidHelper = new DroidHelper (this);
				break;
			}

			Logger = new TestLogger (new ConsoleLogger (this));

			if (Options.JenkinsHtml != null)
				JenkinsHtml = new StreamWriter (Options.JenkinsHtml);

			if (Launcher != null) {
				LauncherOptions = new LauncherOptions {
					Category = Options.Category, Features = Options.Features
				};
			}
		}

		void Finish ()
		{
			if (JenkinsHtml != null) {
				JenkinsHtml.Flush ();
				JenkinsHtml.Dispose ();
			}
		}

		void LogInfo (string message)
		{
			if (Options.Jenkins)
				System.Console.WriteLine ($"[info] {message}");
			Debug (message);
		}

		void LogError (string message, Exception error = null)
		{
			if (Options.Jenkins) {
				System.Console.WriteLine ($"[error] {message}");
				if (error != null)
					System.Console.WriteLine ($"[error] {error}");
			} else {
				PrintError (message);
				if (error != null)
					PrintError (error.ToString ());
			}

			if (JenkinsHtml != null) {
				JenkinsHtml.WriteLine ("<p><b>{message}</b></p>");
				if (error != null)
					JenkinsHtml.WriteLine ("<pre>{error}</pre>");
			}
		}

		void LogException (Exception ex)
		{
			var aggregate = ex as AggregateException;
			if (aggregate != null && aggregate.InnerExceptions.Count == 1) {
				LogException (aggregate.InnerException);
				return;
			}

			var toolEx = ex as ExternalToolException;
			if (toolEx != null) {
				if (!string.IsNullOrEmpty (toolEx.ErrorOutput))
					LogError ($"ERROR: External tool '{toolEx.Tool}' failed:\n{toolEx.ErrorOutput}\n");
				else
					LogError ($"ERROR: External tool '{toolEx.Tool}' failed:\n{toolEx}\n");
				return;
			}

			LogError ($"ERROR: {ex.Message}", ex);
		}

		internal static void WriteLine ()
		{
			global::System.Console.WriteLine();
		}

		internal static void WriteLine (string message, params object[] args)
		{
			global::System.Console.WriteLine (message, args);
		}

		internal static void Debug (string message)
		{
			SD.Debug.WriteLine (message);
		}

		internal static void Debug (string message, params object[] args)
		{
			Debug (string.Format (message, args));
		}

		internal static void PrintError (string message, params object[] args)
		{
			System.Console.Error.WriteLine (string.Format (message, args));
		}

		internal static IPEndPoint GetEndPoint (string text)
		{
			int port;
			string host;
			var pos = text.IndexOf (":");
			if (pos < 0) {
				host = text;
				port = 8888;
			} else {
				host = text.Substring (0, pos);
				port = int.Parse (text.Substring (pos + 1));
			}

			var address = IPAddress.Parse (host);
			return new IPEndPoint (address, port);
		}

		internal static IPEndPoint GetLocalEndPoint ()
		{
			return new IPEndPoint (PortableSupportImpl.LocalAddress, 11111);
		}

		static string PrintEndPoint (IPEndPoint endpoint)
		{
			return string.Format ("{0}:{1}", endpoint.Address, endpoint.Port);
		}

		static IPortableEndPoint GetPortableEndPoint (IPEndPoint endpoint)
		{
			var support = DependencyInjector.Get<IPortableEndPointSupport> ();
			return support.GetEndpoint (endpoint.Address.ToString (), endpoint.Port);
		}

		async Task<int> Run (CancellationToken cancellationToken)
		{
			LogInfo ("Running test suite.");

			bool success = false;
			int? exitCode = null;

			switch (Options.Command) {
			case Command.Local:
				exitCode = await RunLocal (cancellationToken).ConfigureAwait (false);
				break;
			case Command.Connect:
				exitCode = await ConnectToServer (cancellationToken).ConfigureAwait (false);
				break;
			case Command.Gui:
				exitCode = await ConnectToGui (cancellationToken).ConfigureAwait (false);
				break;
			case Command.Listen:
				exitCode = await WaitForConnection (cancellationToken).ConfigureAwait (false);
				break;
			case Command.Simulator:
			case Command.Device:
			case Command.Mac:
			case Command.Android:
			case Command.TVOS:
				exitCode = await LaunchApplication (cancellationToken).ConfigureAwait (false);
				break;
			case Command.Avd:
				success = await DroidHelper.CheckAvd (cancellationToken).ConfigureAwait (false);
				break;
			case Command.Emulator:
				success = await DroidHelper.CheckEmulator (cancellationToken).ConfigureAwait (false);
				break;
			case Command.Apk:
				await DroidHelper.InstallApk (Options.Application, cancellationToken).ConfigureAwait (false);
				success = true;
				break;
			case Command.Result:
				success = await ShowResult (cancellationToken).ConfigureAwait (false);
				break;
			default:
				throw new NotImplementedException ();
			}

			return exitCode ?? (success ? 0 : 1);
		}

		async Task<int> ConnectToGui (CancellationToken cancellationToken)
		{
			var framework = TestFramework.GetLocalFramework (PackageName, Assembly, Options.Dependencies);

			TestServer server;
			try {
				var endpoint = GetPortableEndPoint (Options.GuiEndPoint);
				server = await TestServer.ConnectToGui (this, endpoint, framework, cancellationToken);
			} catch (SocketException ex) {
				if (ex.SocketErrorCode == SocketError.ConnectionRefused && Options.OptionalGui) {
					return await RunLocal (cancellationToken);
				}
				throw;
			}

			Options.UpdateConfiguration (server.Session);

			cancellationToken.ThrowIfCancellationRequested ();
			await server.WaitForExit (cancellationToken);
			return 0;
		}

		int ExitCodeForResult {
			get {
				switch (result.Status) {
				case TestStatus.Success:
					return 0;
				case TestStatus.Unstable:
					return 2;
				case TestStatus.Canceled:
					return 3;
				default:
					return 1;
				}
			}
		}

		async Task<int> RunLocal (CancellationToken cancellationToken)
		{
			var framework = TestFramework.GetLocalFramework (PackageName, Assembly, Options.Dependencies);

			cancellationToken.ThrowIfCancellationRequested ();
			session = TestSession.CreateLocal (this, framework);
			Options.UpdateConfiguration (session);

			PrintConfigSummary (session.Configuration);

			var test = session.RootTestCase;

			Debug ("Got test: {0}", test.Path.FullName);
			startTime = DateTime.Now;
			result = await session.Run (test, cancellationToken);
			endTime = DateTime.Now;
			Debug ("Got result: {0} {1}", result.Status, test.Path.FullName);

			SaveResult (session);

			return ExitCodeForResult;
		}

		async Task<int> ConnectToServer (CancellationToken cancellationToken)
		{
			var endpoint = GetPortableEndPoint (Options.EndPoint);
			var server = await TestServer.ConnectToServer (this, endpoint, cancellationToken);
			cancellationToken.ThrowIfCancellationRequested ();

			session = server.Session;
			if (Options.UpdateConfiguration (session))
				await session.UpdateSettings (cancellationToken);

			PrintConfigSummary (session.Configuration);

			var test = session.RootTestCase;
			cancellationToken.ThrowIfCancellationRequested ();

			Debug ("Got test: {0}", test.Path.FullName);
			startTime = DateTime.Now;
			result = await session.Run (test, cancellationToken);
			endTime = DateTime.Now;
			cancellationToken.ThrowIfCancellationRequested ();
			Debug ("Got result: {0} {1}", result.Status, test.Path.FullName);

			SaveResult (session);

			await server.Stop (cancellationToken);

			return ExitCodeForResult;
		}

		async Task<int> LaunchApplication (CancellationToken cancellationToken)
		{
			var endpoint = GetPortableEndPoint (Options.EndPoint);

			TestServer server;
			try {
				server = await TestServer.LaunchApplication (this, endpoint, Launcher, LauncherOptions, cancellationToken);
			} catch (LauncherErrorException ex) {
				LogException (ex);
				Environment.Exit (255);
				throw;
			}

			cancellationToken.ThrowIfCancellationRequested ();

			Debug ("Test app launched.");
			var exitCode = await RunRemoteSession (server, cancellationToken);

			Debug ("Application finished.");

			return exitCode;
		}

		async Task<int> WaitForConnection (CancellationToken cancellationToken)
		{
			var endpoint = GetPortableEndPoint (Options.EndPoint);
			var server = await TestServer.WaitForConnection (this, endpoint, cancellationToken);
			cancellationToken.ThrowIfCancellationRequested ();

			Debug ("Got server connection.");
			return await RunRemoteSession (server, cancellationToken);
		}

		async Task<int> RunRemoteSession (TestServer server, CancellationToken cancellationToken)
		{
			session = server.Session;
			if (Options.UpdateConfiguration (session))
				await session.UpdateSettings (cancellationToken);

			PrintConfigSummary (session.Configuration);

			var test = session.RootTestCase;
			cancellationToken.ThrowIfCancellationRequested ();

			Debug ("Got test: {0}", test.Path.FullName);
			startTime = DateTime.Now;
			result = await session.Run (test, cancellationToken);
			endTime = DateTime.Now;
			cancellationToken.ThrowIfCancellationRequested ();
			Debug ("Got result: {0} {1}", result.Status, result.Path.FullName);

			SaveResult (session);

			await server.Stop (cancellationToken);

			return ExitCodeForResult;
		}

		void PrintConfigSummary (TestConfiguration config)
		{
			var category = config.CurrentCategory.Name;
			Debug ($"Test Category: {category}");
			var sb = new StringBuilder ();
			foreach (var feature in config.Features) {
				if (!feature.CanModify || feature.Constant != null)
					continue;
				var defaultValue = feature.DefaultValue ?? false;
				var enabled = config.IsEnabled (feature);
				if (enabled == defaultValue)
					continue;
				var prefix = enabled ? "+" : "-";
				Debug ($"Test Feature: {prefix}{feature.Name}");
				if (sb.Length > 0)
					sb.Append (", ");
				sb.Append ($"{prefix}{feature.Name}");
			}
			var categoryText = category;
			if (sb.Length > 0)
				categoryText += $" ({sb})";
			
			if (JenkinsHtml != null)
				JenkinsHtml.WriteLine ($"<h3>Test Category: {categoryText}</h3>");
		}

		void SaveResult (TestSession session)
		{
			LogInfo ($"{countTests} tests, {countSuccess} passed, {countErrors} errors, {countUnstable} unstable, {countIgnored} ignored.");
			LogInfo ($"Total time: {endTime - startTime}.");
			if (JenkinsHtml != null) {
				JenkinsHtml.WriteLine ($"<p>{countTests} tests, {countSuccess} passed, {countErrors} errors, {countUnstable} unstable, {countIgnored} ignored.");
				JenkinsHtml.WriteLine ($"<br>Total time: {endTime - startTime}.</p>");
			}

			if (Options.ResultOutput != null) {
				var serialized = TestSerializer.WriteTestResult (result);
				var settings = new XmlWriterSettings ();
				settings.Indent = true;
				using (var writer = XmlTextWriter.Create (Options.ResultOutput, settings))
					serialized.WriteTo (writer);
				Debug ("Result written to {0}.", Options.ResultOutput);
			}

			if (Options.JUnitResultOutput != null) {
				JUnitResultPrinter.Print (result, Options.JUnitResultOutput);
				Debug ("JUnit result written to {0}.", Options.JUnitResultOutput);
			}

			var printer = new ResultPrinter (global::System.Console.Out, result);
			printer.Print ();
		}

		async Task<bool> ShowResult (CancellationToken cancellationToken)
		{
			await Task.Yield ();

			var printer = ResultPrinter.Load (global::System.Console.Out, Options.ResultOutput);
			var ret = printer.Print ();

			if (Options.JUnitResultOutput != null) {
				JUnitResultPrinter.Print (printer.Result, Options.JUnitResultOutput);
				Debug ("JUnit result written to {0}.", Options.JUnitResultOutput);
			}

			return ret;
		}

		void OnLogMessage (string message)
		{
			Debug (message);
		}

		void OnLogMessage (string format, params object[] args)
		{
			OnLogMessage (string.Format (format, args));
		}

		void OnLogDebug (int level, string message)
		{
			if (Settings.LocalLogLevel >= 0 && level > Settings.LocalLogLevel)
				return;
			Debug (message);
		}

		int countTests;
		int countSuccess;
		int countErrors;
		int countUnstable;
		int countIgnored;

		void OnStatisticsEvent (TestLoggerBackend.StatisticsEventArgs args)
		{
			switch (args.Type) {
			case TestLoggerBackend.StatisticsEventType.Running:
				++countTests;
				Debug ("Running {0}", args.Name);
				break;
			case TestLoggerBackend.StatisticsEventType.Finished:
				switch (args.Status) {
				case TestStatus.Success:
					++countSuccess;
					break;
				case TestStatus.Ignored:
				case TestStatus.None:
					++countIgnored;
					break;
				case TestStatus.Unstable:
					++countUnstable;
					break;
				default:
					++countErrors;
					break;
				}

				Debug ("Finished {0}: {1}", args.Name, args.Status);
				break;
			case TestLoggerBackend.StatisticsEventType.Reset:
				break;
			}
		}

		class ConsoleLogger : TestLoggerBackend
		{
			readonly Program Program;

			public ConsoleLogger (Program program)
			{
				Program = program;
			}

			protected override void OnLogEvent (LogEntry entry)
			{
				switch (entry.Kind) {
				case EntryKind.Debug:
					Program.OnLogDebug (entry.LogLevel, entry.Text);
					break;

				case EntryKind.Error:
					if (entry.Error != null)
						Program.OnLogMessage ("ERROR: {0}\n", entry.Error);
					else
						Program.OnLogMessage (entry.Text);
					break;

				default:
					Program.OnLogMessage (entry.Text);
					break;
				}
			}

			protected override void OnStatisticsEvent (StatisticsEventArgs args)
			{
				Program.OnStatisticsEvent (args);
			}
		}
	}
}

