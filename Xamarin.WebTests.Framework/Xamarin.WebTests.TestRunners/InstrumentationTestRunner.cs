﻿//
// InstrumentationTestRunner.cs
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
using System.IO;
using System.Text;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using System.Collections;
using System.Collections.Generic;
using Xamarin.AsyncTests;
using Xamarin.AsyncTests.Constraints;
using Xamarin.AsyncTests.Portable;

namespace Xamarin.WebTests.TestRunners
{
	using ConnectionFramework;
	using HttpFramework;
	using HttpHandlers;
	using TestFramework;
	using HttpClient;
	using Resources;

	public abstract class InstrumentationTestRunner : AbstractConnection
	{
		public HttpServerProvider Provider {
			get;
		}

		internal Uri Uri => Provider.Uri;

		internal HttpServerFlags ServerFlags {
			get;
		}

		public HttpServer Server {
			get;
		}

		public string ME {
			get;
		}

		public InstrumentationTestRunner (HttpServerProvider provider, string identifier)
		{
			Provider = provider;
			ServerFlags = provider.ServerFlags | HttpServerFlags.InstrumentationListener;
			ME = $"{GetType ().Name}({identifier})";

			var parameters = GetParameters (identifier);

			Server = new BuiltinHttpServer (provider.Uri, provider.EndPoint, ServerFlags, parameters, null);
		}

		static ConnectionParameters GetParameters (string identifier)
		{
			var certificateProvider = DependencyInjector.Get<ICertificateProvider> ();
			var acceptAll = certificateProvider.AcceptAll ();

			return new ConnectionParameters (identifier, ResourceManager.SelfSignedServerCertificate) {
				ClientCertificateValidator = acceptAll
			};
		}

		InstrumentationOperation currentOperation;
		InstrumentationOperation queuedOperation;
		volatile int readHandlerCalled;

		protected InstrumentationOperation PrimaryOperation => currentOperation;
		protected InstrumentationOperation QueuedOperation => queuedOperation;
		protected int ReadHandlerCalled => readHandlerCalled;

		public async Task Run (TestContext ctx, CancellationToken cancellationToken)
		{
			var me = $"{ME}.{nameof (Run)}()";
			ctx.LogDebug (2, $"{me}");

			var (handler, flags) = CreateHandler (ctx, true);

			ctx.LogDebug (2, $"{me}");

			currentOperation = CreateOperation (
				ctx, handler, InstrumentationOperationType.Primary, flags);

			currentOperation.Start (ctx, cancellationToken);

			try {
				await currentOperation.WaitForCompletion ().ConfigureAwait (false);
				ctx.LogDebug (2, $"{me} operation done");
			} catch (Exception ex) {
				ctx.LogDebug (2, $"{me} operation failed: {ex.Message}");
				throw;
			}

			await RunSecondary (ctx, cancellationToken);

			if (QueuedOperation != null) {
				ctx.LogDebug (2, $"{me} waiting for queued operations.");
				try {
					await QueuedOperation.WaitForCompletion ().ConfigureAwait (false);
					ctx.LogDebug (2, $"{me} done waiting for queued operations.");
				} catch (Exception ex) {
					ctx.LogDebug (2, $"{me} waiting for queued operations failed: {ex.Message}.");
					throw;
				}
			}

			Server.CloseAll ();
		}

		protected virtual Task RunSecondary (TestContext ctx, CancellationToken cancellationToken)
		{
			return FinishedTask;
		}

		protected abstract (Handler handler, HttpOperationFlags flags) CreateHandler (TestContext ctx, bool primary);

		protected abstract InstrumentationOperation CreateOperation (TestContext ctx, Handler handler, InstrumentationOperationType type,
									     HttpOperationFlags flags);

		protected InstrumentationOperation StartOperation (TestContext ctx, CancellationToken cancellationToken, Handler handler,
		                                                   InstrumentationOperationType type, HttpOperationFlags flags)
		{
			var operation = CreateOperation (ctx, handler, type, flags);
			if (type == InstrumentationOperationType.Queued) {
				if (Interlocked.CompareExchange (ref queuedOperation, operation, null) != null)
					throw new InvalidOperationException ("Invalid nested call.");
			}
			operation.Start (ctx, cancellationToken);
			return operation;
		}

		protected override async Task Initialize (TestContext ctx, CancellationToken cancellationToken)
		{
			await Server.Initialize (ctx, cancellationToken).ConfigureAwait (false);
		}

		protected override async Task Destroy (TestContext ctx, CancellationToken cancellationToken)
		{
			currentOperation?.Dispose ();
			currentOperation = null;
			queuedOperation?.Dispose ();
			queuedOperation = null;
			await Server.Destroy (ctx, cancellationToken).ConfigureAwait (false);
		}

		protected override async Task PreRun (TestContext ctx, CancellationToken cancellationToken)
		{
			await Server.PreRun (ctx, cancellationToken).ConfigureAwait (false);
		}

		protected override async Task PostRun (TestContext ctx, CancellationToken cancellationToken)
		{
			await Server.PostRun (ctx, cancellationToken).ConfigureAwait (false);
		}

		protected override void Stop ()
		{
		}

		protected virtual Task PrimaryReadHandler (TestContext ctx, CancellationToken cancellationToken)
		{
			return FinishedTask;
		}

		protected virtual Task SecondaryReadHandler (TestContext ctx, CancellationToken cancellationToken)
		{
			return FinishedTask;
		}

		protected enum InstrumentationOperationType
		{
			Primary,
			Queued,
			Parallel
		}

		protected abstract class InstrumentationOperation : HttpOperation
		{
			public InstrumentationTestRunner Parent {
				get;
			}

			public InstrumentationOperationType Type {
				get;
			}

			public InstrumentationOperation (InstrumentationTestRunner parent, string me, Handler handler,
			                                 InstrumentationOperationType type, HttpOperationFlags flags,
			                                 HttpStatusCode expectedStatus, WebExceptionStatus expectedError)
				: base (parent.Server, me, handler, flags, expectedStatus, expectedError)
			{
				Parent = parent;
				Type = type;
			}

			StreamInstrumentation instrumentation;

			internal override Stream CreateNetworkStream (TestContext ctx, Socket socket, bool ownsSocket)
			{
				instrumentation = new StreamInstrumentation (ctx, ME, socket, ownsSocket);

				ConfigureNetworkStream (ctx, instrumentation);

				return instrumentation;
			}

			protected abstract void ConfigureNetworkStream (TestContext ctx, StreamInstrumentation instrumentation);

			protected void InstallReadHandler (TestContext ctx)
			{
				instrumentation.OnNextRead ((b, o, s, f, c) => ReadHandler (ctx, b, o, s, f, c));
			}

			async Task<int> ReadHandler (TestContext ctx,
						     byte [] buffer, int offset, int size,
						     StreamInstrumentation.AsyncReadFunc func,
						     CancellationToken cancellationToken)
			{
				cancellationToken.ThrowIfCancellationRequested ();

				var ret = await func (buffer, offset, size, cancellationToken).ConfigureAwait (false);

				Interlocked.Increment (ref Parent.readHandlerCalled);

				await ReadHandler (ctx, buffer, offset, size, ret, cancellationToken);

				return ret;
			}

			protected virtual Task ReadHandler (TestContext ctx, byte [] buffer, int offset, int size, int ret, CancellationToken cancellationToken)
			{
				return Type == InstrumentationOperationType.Primary ? Parent.PrimaryReadHandler (ctx, cancellationToken) : Parent.SecondaryReadHandler (ctx, cancellationToken);
			}

			protected override void Destroy ()
			{
				instrumentation?.Dispose ();
				instrumentation = null;
			}
		}
	}
}
