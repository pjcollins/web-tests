//
// RenegotiationTestRunner.cs
//
// Author:
//       Martin Baulig <mabaul@microsoft.com>
//
// Copyright (c) 2017 Xamarin Inc. (http://www.xamarin.com)
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
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Collections.Generic;
using Xamarin.AsyncTests;
using Xamarin.AsyncTests.Constraints;

namespace Xamarin.WebTests.MonoTestFramework
{
	using MonoTestFeatures;
	using MonoConnectionFramework;
	using ConnectionFramework;
	using TestFramework;
	using Resources;
	using System.Security.Authentication;
	using Mono.Security.Interface;

	[RenegotiationTestRunner]
	public class RenegotiationTestRunner : ClientAndServer
	{
		new public RenegotiationTestParameters Parameters => (RenegotiationTestParameters)base.Parameters;

		internal MonoServer MonoServer => (MonoServer)Server;

		internal MonoClient MonoClient => (MonoClient)Client;

		public RenegotiationTestType EffectiveType {
			get {
				if (Parameters.Type == RenegotiationTestType.MartinTest)
					return MartinTest;
				return Parameters.Type;
			}
		}

		public ConnectionHandler ConnectionHandler {
			get;
		}

		public string ME {
			get;
		}

		public RenegotiationTestRunner (Connection server, Connection client, MonoConnectionTestProvider provider,
		                                RenegotiationTestParameters parameters)
			: base (server, client, parameters)
		{
			ConnectionHandler = new DefaultConnectionHandler (this);
			ME = $"{GetType ().Name}({EffectiveType})";
		}

		const RenegotiationTestType MartinTest = RenegotiationTestType.MartinTest;

		public static IEnumerable<RenegotiationTestType> GetRenegotiationTestTypes (TestContext ctx, MonoConnectionTestCategory category)
		{
			var setup = DependencyInjector.Get<IMonoConnectionFrameworkSetup> ();

			switch (category) {
			case MonoConnectionTestCategory.MartinTest:
				yield return RenegotiationTestType.MartinTest;
				yield break;

			default:
				throw ctx.AssertFail (category);
			}
		}

		static string GetTestName (MonoConnectionTestCategory category, RenegotiationTestType type, params object[] args)
		{
			var sb = new StringBuilder ();
			sb.Append (type);
			foreach (var arg in args) {
				sb.AppendFormat (":{0}", arg);
			}
			return sb.ToString ();
		}

		public static RenegotiationTestParameters GetParameters (TestContext ctx, MonoConnectionTestCategory category,
		                                                         RenegotiationTestType type)
		{
			var certificateProvider = DependencyInjector.Get<ICertificateProvider> ();
			var acceptAll = certificateProvider.AcceptAll ();

			var name = GetTestName (category, type);

			return new RenegotiationTestParameters (category, type, name, ResourceManager.SelfSignedServerCertificate) {
				ClientCertificateValidator = acceptAll
			};
		}

		protected override Task PreRun (TestContext ctx, CancellationToken cancellationToken)
		{
			return base.PreRun (ctx, cancellationToken);
		}

		protected override Task PostRun (TestContext ctx, CancellationToken cancellationToken)
		{
			return base.PostRun (ctx, cancellationToken);
		}

		protected override void InitializeConnection (TestContext ctx)
		{
			ConnectionHandler.InitializeConnection (ctx);
			base.InitializeConnection (ctx);
		}

		protected sealed override async Task MainLoop (TestContext ctx, CancellationToken cancellationToken)
		{
			var me = $"{ME}.{nameof (MainLoop)}";
			ctx.LogDebug (4, $"{me}");

			ctx.LogDebug (4, $"{me} - client={MonoClient.Provider} server={MonoServer.Provider} - {MonoServer.CanRenegotiate}");
			ctx.Assert (MonoServer.CanRenegotiate, "MonoServer.CanRenegotiate");

			var buffer = new byte[16];
			var readTask = Client.Stream.ReadAsync (buffer, 0, buffer.Length, cancellationToken);

			var renegotiateTask = MonoServer.RenegotiateAsync (cancellationToken);

			ctx.LogDebug (4, $"{me} - called Renegotiate()");

			try {
				await Task.WhenAll (readTask, renegotiateTask).ConfigureAwait (false);
				throw ctx.AssertFail ("Expected AuthenticationException/NoRenegotiation");
			} catch (Exception ex) {
				ctx.Assert (ex, Is.InstanceOf<AuthenticationException> ());
				ctx.Assert (ex.InnerException, Is.InstanceOf <TlsException> ());
				var tlsExc = (TlsException)ex.InnerException;
				ctx.Assert (tlsExc.Alert.Description, Is.EqualTo (AlertDescription.NoRenegotiation));
			}

			ctx.LogDebug (4, $"{me} - after wait");

			await ConnectionHandler.MainLoop (ctx, cancellationToken);
		}

		void LogDebug (TestContext ctx, int level, string message, params object[] args)
		{
			var formatted = string.Format (message, args);
			ctx.LogDebug (level, $"RenegotiationTestRunner({EffectiveType}): {formatted}");
		}

		protected override Task StartClient (TestContext ctx, CancellationToken cancellationToken)
		{
			return Client.Start (ctx, null, cancellationToken);
		}

		protected override Task StartServer (TestContext ctx, CancellationToken cancellationToken)
		{
			return Server.Start (ctx, null, cancellationToken);
		}

		protected override Task ClientShutdown (TestContext ctx, CancellationToken cancellationToken)
		{
			return Client.Shutdown (ctx, cancellationToken);
		}

		protected override Task ServerShutdown (TestContext ctx, CancellationToken cancellationToken)
		{
			return Server.Shutdown (ctx, cancellationToken);
		}
	}
}
