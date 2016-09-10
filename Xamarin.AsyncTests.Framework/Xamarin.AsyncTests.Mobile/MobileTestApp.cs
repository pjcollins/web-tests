﻿//
// MobileTestApp.cs
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
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Collections.Generic;
using SD = System.Diagnostics;
using Xamarin.AsyncTests;
using Xamarin.AsyncTests.Framework;
using Xamarin.AsyncTests.Remoting;
using Xamarin.AsyncTests.Portable;

namespace Xamarin.AsyncTests.Mobile
{
	public class MobileTestApp : TestApp
	{
		public ISimpleUIController Controller {
			get;
			private set;
		}

		public TestFramework Framework {
			get;
			private set;
		}

		public MobileSessionMode SessionMode {
			get;
			private set;
		}

		public IPortableEndPoint EndPoint {
			get;
			private set;
		}

		public TestLogger Logger {
			get;
			private set;
		}

		public SettingsBag Settings {
			get;
			private set;
		}

		public event EventHandler FinishedEvent;

		public MobileTestApp (ISimpleUIController controller, TestFramework framework, string options)
		{
			Controller = controller;
			Framework = framework;

			Settings = SettingsBag.CreateDefault ();
			Settings.LocalLogLevel = -1;

			ParseSessionMode (options);

			Logger = new TestLogger (new MobileLogger (this));

			Controller.CategoryChangedEvent += (sender, e) => OnCategoryChanged (e);

			Controller.SessionChangedEvent += (sender, e) => OnSessionChanged ();
		}

		int? logLevel;
		bool debugMode;
		string category;
		string features;
		string customSettings;

		void ParseSessionMode (string options)
		{
			if (string.IsNullOrEmpty (options)) {
				SessionMode = MobileSessionMode.Local;
				return;
			}

			Debug ("Got XAMARIN_ASYNCTESTS_OPTIONS argument: '{0}'.", options);

			var p = new NDesk.Options.OptionSet ();
			p.Add ("debug", v => debugMode = true);
			p.Add ("log-level=", v => logLevel = int.Parse (v));
			p.Add ("category=", v => category = v);
			p.Add ("features=", v => features = v);
			p.Add ("set=", v => customSettings = v);

			var optArray = options.Split (new char [] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
			var args = p.Parse (optArray);

			if (debugMode) {
				Settings.LogLevel = -1;
				Settings.LocalLogLevel = -1;
				Settings.DisableTimeouts = true;
			}

			if (logLevel != null)
				Settings.LogLevel = logLevel.Value;

			if (customSettings != null)
				ParseSettings (customSettings);

			if (args.Count == 0) {
				SessionMode = MobileSessionMode.Local;
				return;
			}

			if (args [0] == "server")
				SessionMode = MobileSessionMode.Server;
			else if (args [0] == "connect") {
				SessionMode = MobileSessionMode.Connect;
			} else if (args [0] == "local") {
				SessionMode = MobileSessionMode.Local;
				if (args.Count != 1)
					throw new InvalidOperationException ("Invalid 'XAMARIN_ASYNCTESTS_OPTIONS' argument.");
				return;
			} else
				throw new InvalidOperationException ("Invalid 'XAMARIN_ASYNCTESTS_OPTIONS' argument.");

			if (args.Count == 2) {
				EndPoint = DependencyInjector.Get<IPortableEndPointSupport> ().ParseEndpoint (args [1]);
			} else if (args.Count == 1) {
				EndPoint = GetEndPoint ();
			} else {
				throw new InvalidOperationException ("Invalid 'XAMARIN_ASYNCTESTS_OPTIONS' argument.");
			}
		}

		void ParseSettings (string arg)
		{
			var parts = arg.Split (',');
			foreach (var part in parts) {
				var pos = part.IndexOf ('=');
				if (pos > 0) {
					var key = part.Substring (0, pos);
					var value = part.Substring (pos + 1);
					Debug ("SET: |{0}|{1}|", key, value);
					if (key [0] == '-')
						throw new InvalidOperationException ();
					Settings.SetValue (key, value);
				} else if (part [0] == '-') {
					var key = part.Substring (1);
					Settings.RemoveValue (key);
				} else {
					throw new InvalidOperationException ();
				}
			}
		}

		bool ModifyConfiguration (TestConfiguration config)
		{
			bool modified = false;

			if (category != null) {
				if (string.Equals (category, "all", StringComparison.OrdinalIgnoreCase))
					config.CurrentCategory = TestCategory.All;
				if (string.Equals (category, "global", StringComparison.OrdinalIgnoreCase))
					config.CurrentCategory = TestCategory.Global;
				else
					config.CurrentCategory = config.Categories.First (c => c.Name.Equals (category));
				modified = true;
			}

			if (features != null) {
				modified = true;
				var parts = features.Split (',');
				foreach (var part in parts) {
					var name = part;
					bool enable = true;
					if (part [0] == '-') {
						name = part.Substring (1);
						enable = false;
					} else if (part [0] == '+') {
						name = part.Substring (1);
						enable = true;
					}

					if (name.Equals ("all")) {
						foreach (var feature in config.Features) {
							if (feature.CanModify)
								config.SetIsEnabled (feature, enable);
						}
					} else {
						var feature = config.Features.First (f => f.Name.Equals (name));
						config.SetIsEnabled (feature, enable);
					}
				}
			}

			return modified;
		}

		public Task Run ()
		{
			return OnRun ();
		}

		CancellationTokenSource cts;

		async Task OnRun ()
		{
			await Task.Yield ();

			if (Interlocked.CompareExchange (ref cts, new CancellationTokenSource (), null) != null)
				return;

			try {
				Controller.Message ("Running.");
				Controller.IsRunning = true;

				var cancellationToken = cts.Token;

				OnResetStatistics ();

				cancellationToken.ThrowIfCancellationRequested ();
				await session.Run (session.RootTestCase, cancellationToken);

				Debug ("{0} test run, {1} ignored, {2} passed, {3} errors.",
				       countTests, countIgnored, countSuccess, countErrors);
			} finally {
				var oldCts = Interlocked.Exchange (ref cts, null);
				if (oldCts != null)
					oldCts.Dispose ();
				Controller.Message ("Done running.");
				Controller.IsRunning = false;
			}
		}

		async Task OnConnect ()
		{
			await Task.Yield ();

			if (Interlocked.CompareExchange (ref cts, new CancellationTokenSource (), null) != null)
				return;

			try {
				Controller.IsRunning = true;

				var cancellationToken = cts.Token;

				OnResetStatistics ();

				cancellationToken.ThrowIfCancellationRequested ();
				await session.Run (session.RootTestCase, cancellationToken);

				cancellationToken.ThrowIfCancellationRequested ();
				var running = await server.WaitForExit (cancellationToken);
				Debug ("WAIT FOR EXIT: {0}", running);

				Debug ("{0} test run, {1} ignored, {2} passed, {3} errors.",
					countTests, countIgnored, countSuccess, countErrors);

				await server.Stop (cancellationToken);
			} finally {
				var oldCts = Interlocked.Exchange (ref cts, null);
				if (oldCts != null)
					oldCts.Dispose ();
				Controller.IsRunning = false;
				if (SessionMode != MobileSessionMode.Connect)
					Controller.CanRun = true;
			}
		}

		public void Stop ()
		{
			OnStop ();
		}

		void OnStop ()
		{
			var oldCts = Interlocked.Exchange (ref cts, null);
			if (oldCts == null)
				return;

			oldCts.Cancel ();
			oldCts.Dispose ();
		}

		static IPortableEndPoint GetEndPoint ()
		{
			var support = DependencyInjector.Get<IPortableEndPointSupport> ();
			return support.GetEndpoint (8888);
		}

		TestServer server;
		TestSession session;

		public async void Start ()
		{
			bool finished;
			do {
				finished = await StartServer (CancellationToken.None);
				if (SessionMode == MobileSessionMode.Connect) {
					if (FinishedEvent != null)
						FinishedEvent (this, EventArgs.Empty);
					return;
				}
			} while (finished);
		}

		async Task<bool> StartServer (CancellationToken cancellationToken)
		{
			switch (SessionMode) {
			case MobileSessionMode.Local:
				server = await TestServer.StartLocal (this, Framework, cancellationToken);
				Controller.Message ("started local server.");
				break;

			case MobileSessionMode.Server:
				Controller.Message ("Listening at is {0}:{1}.", EndPoint.Address, EndPoint.Port);
				server = await TestServer.StartServer (this, EndPoint, Framework, cancellationToken);
				break;

			case MobileSessionMode.Connect:
				Controller.Message ("Connecting to {0}:{1}.", EndPoint.Address, EndPoint.Port);
				server = await TestServer.ConnectToRemote (this, EndPoint, Framework, cancellationToken);
				break;

			default:
				throw new NotImplementedException ();
			}

			Debug ("Got server: {0}", server);

			session = await server.GetTestSession (CancellationToken.None);
			OnSessionChanged ();
			Controller.Message ("Got test session {0} from {1}.", session.Name, server.App);

			Debug ("Got test session: {0}", session);

			OnResetStatistics ();

			if (SessionMode == MobileSessionMode.Local) {
				Controller.CanRun = true;
				return false;
			}

			var running = await server.WaitForExit (CancellationToken.None);
			Debug ("Wait for exit: {0}", running);

			if (running && SessionMode != MobileSessionMode.Connect) {
				Controller.CanRun = true;
				return false;
			}

			Debug ("{0} test run, {1} ignored, {2} passed, {3} errors.",
				countTests, countIgnored, countSuccess, countErrors);

			try {
				await server.Stop (CancellationToken.None);
			} catch (Exception ex) {
				Debug ("Failed to stop server: {0}", ex.Message);
			}

			Controller.Message ("Done running.");
			return true;
		}

		void Debug (string format, params object [] args)
		{
			Debug (string.Format (format, args));
		}

		void Debug (string message)
		{
			Controller.DebugMessage (message);
		}

		void OnLogMessage (string message)
		{
			Debug (message);
		}

		void OnLogDebug (int level, string message)
		{
			if (Settings.LocalLogLevel >= 0 && level > Settings.LocalLogLevel)
				return;
			Debug (message);
		}

		bool suppressCategoryChange;

		void OnSessionChanged ()
		{
			try {
				suppressCategoryChange = true;

				var categories = new List<string> ();
				categories.Add ("All");

				var selected = 0;

				Debug ("ON SESSION CHANGED: {0}", session);

				if (session == null) {
					Controller.SetCategories (categories, 0);
					return;
				}

				ModifyConfiguration (session.Configuration);

				foreach (var item in session.Configuration.Categories) {
					categories.Add (item.Name);
					if (item == session.Configuration.CurrentCategory)
						selected = categories.Count - 1;
				}
				Controller.SetCategories (categories, selected);
			} finally {
				suppressCategoryChange = false;
			}
		}

		void OnCategoryChanged (int selectedIdx)
		{
			if (suppressCategoryChange || session == null)
				return;

			if (selectedIdx <= 0) {
				session.Configuration.CurrentCategory = TestCategory.All;
				return;
			}

			var selected = Controller.Categories [selectedIdx];
			var lookup = session.Configuration.Categories.FirstOrDefault (c => c.Name.Equals (selected));
			session.Configuration.CurrentCategory = lookup ?? TestCategory.All;
		}

		void OnResetStatistics ()
		{
			Controller.StatusMessage (string.Empty);
			Controller.StatisticsMessage (string.Empty);
			countTests = countSuccess = countErrors = countIgnored = 0;
		}

		int countTests;
		int countSuccess;
		int countErrors;
		int countIgnored;

		void OnStatisticsEvent (TestLoggerBackend.StatisticsEventArgs args)
		{
			switch (args.Type) {
			case TestLoggerBackend.StatisticsEventType.Running:
				++countTests;
				Debug ("Running {0}", args.Name);
				Controller.StatusMessage ("Running {0}", args.Name);
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
				default:
					++countErrors;
					break;
				}

				Debug ("Finished {0}: {1}", args.Name, args.Status);

				Controller.StatusMessage ("Finished {0}: {1}", args.Name, args.Status);
				Controller.StatisticsMessage ("{0} test run, {1} ignored, {2} passed, {3} errors.",
				                              countTests, countIgnored, countSuccess, countErrors);
				break;
			case TestLoggerBackend.StatisticsEventType.Reset:
				OnResetStatistics ();
				break;
			}
		}

		class MobileLogger : TestLoggerBackend
		{
			readonly MobileTestApp MobileApp;

			public MobileLogger (MobileTestApp app)
			{
				MobileApp = app;
			}

			protected internal override void OnLogEvent (LogEntry entry)
			{
				switch (entry.Kind) {
				case EntryKind.Debug:
					MobileApp.OnLogDebug (entry.LogLevel, entry.Text);
					break;

				case EntryKind.Error:
					if (entry.Error != null)
						MobileApp.OnLogMessage (string.Format ("ERROR: {0}", entry.Error));
					else
						MobileApp.OnLogMessage (entry.Text);
					break;

				default:
					MobileApp.OnLogMessage (entry.Text);
					break;
				}
			}

			protected internal override void OnStatisticsEvent (StatisticsEventArgs args)
			{
				MobileApp.OnStatisticsEvent (args);
			}
		}
	}
}

