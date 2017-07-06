//
// HttpInstrumentationTestRunner.cs
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
	using Resources;

	[HttpInstrumentationTestRunner]
	public class HttpInstrumentationTestRunner : AbstractConnection, IHttpServerDelegate, IHttpInstrumentation
	{
		public ConnectionTestProvider Provider {
			get;
		}

		protected Uri Uri {
			get;
		}

		protected HttpServerFlags ServerFlags {
			get;
		}

		new public HttpInstrumentationTestParameters Parameters {
			get { return (HttpInstrumentationTestParameters)base.Parameters; }
		}

		public HttpInstrumentationTestType EffectiveType => GetEffectiveType (Parameters.Type);

		static HttpInstrumentationTestType GetEffectiveType (HttpInstrumentationTestType type)
		{
			if (type == HttpInstrumentationTestType.MartinTest)
				return MartinTest;
			return type;
		}

		public HttpServer Server {
			get;
		}

		public string ME {
			get;
		}

		public HttpInstrumentationTestRunner (IPortableEndPoint endpoint, HttpInstrumentationTestParameters parameters,
						      ConnectionTestProvider provider, Uri uri, HttpServerFlags flags)
			: base (endpoint, parameters)
		{
			Provider = provider;
			ServerFlags = flags;
			Uri = uri;

			Server = new BuiltinHttpServer (uri, endpoint, flags, parameters, null) {
				Delegate = this, Instrumentation = this
			};

			ME = $"{GetType ().Name}({EffectiveType})";
		}

		const HttpInstrumentationTestType MartinTest = HttpInstrumentationTestType.ReadTimeout;

		static readonly HttpInstrumentationTestType[] WorkingTests = {
			HttpInstrumentationTestType.Simple,
			HttpInstrumentationTestType.InvalidDataDuringHandshake,
			HttpInstrumentationTestType.AbortDuringHandshake,
			HttpInstrumentationTestType.ParallelRequests,
			HttpInstrumentationTestType.ThreeParallelRequests,
			HttpInstrumentationTestType.ParallelRequestsSomeQueued,
			HttpInstrumentationTestType.ManyParallelRequests,
			HttpInstrumentationTestType.SimpleQueuedRequest,
			HttpInstrumentationTestType.CancelQueuedRequest,
			HttpInstrumentationTestType.CancelMainWhileQueued,
			HttpInstrumentationTestType.SimpleNtlm,
			HttpInstrumentationTestType.NtlmWhileQueued,
			HttpInstrumentationTestType.ReuseConnection,
			HttpInstrumentationTestType.SimplePost,
			HttpInstrumentationTestType.SimpleRedirect,
			HttpInstrumentationTestType.PostRedirect,
			HttpInstrumentationTestType.PostNtlm,
			HttpInstrumentationTestType.NtlmChunked,
			HttpInstrumentationTestType.Get404,
			HttpInstrumentationTestType.NtlmInstrumentation,
			HttpInstrumentationTestType.LargeHeader,
			HttpInstrumentationTestType.LargeHeader2,
			HttpInstrumentationTestType.SendResponseAsBlob,
			HttpInstrumentationTestType.ReuseAfterPartialRead,
			HttpInstrumentationTestType.CustomConnectionGroup,
			HttpInstrumentationTestType.ReuseCustomConnectionGroup,
			HttpInstrumentationTestType.CloseCustomConnectionGroup,
			HttpInstrumentationTestType.CloseRequestStream
		};

		static readonly HttpInstrumentationTestType[] UnstableTests = {
			HttpInstrumentationTestType.ReuseConnection2,
			HttpInstrumentationTestType.CloseIdleConnection,
			HttpInstrumentationTestType.NtlmClosesConnection,
			HttpInstrumentationTestType.ParallelNtlm
		};

		static readonly HttpInstrumentationTestType[] StressTests = {
			HttpInstrumentationTestType.ManyParallelRequestsStress
		};

		static readonly HttpInstrumentationTestType[] MartinTests = {
			HttpInstrumentationTestType.MartinTest
		};

		public static IList<HttpInstrumentationTestType> GetInstrumentationTypes (TestContext ctx, ConnectionTestCategory category)
		{
			var setup = DependencyInjector.Get<IConnectionFrameworkSetup> ();

			switch (category) {
			case ConnectionTestCategory.MartinTest:
				return MartinTests;

			case ConnectionTestCategory.HttpInstrumentation:
				return WorkingTests;

			case ConnectionTestCategory.HttpInstrumentationStress:
				return StressTests;

			case ConnectionTestCategory.HttpInstrumentationExperimental:
				return UnstableTests;

			default:
				throw ctx.AssertFail (category);
			}
		}

		static string GetTestName (ConnectionTestCategory category, HttpInstrumentationTestType type, params object[] args)
		{
			var sb = new StringBuilder ();
			sb.Append (type);
			foreach (var arg in args) {
				sb.AppendFormat (":{0}", arg);
			}
			return sb.ToString ();
		}

		public static HttpInstrumentationTestParameters GetParameters (TestContext ctx, ConnectionTestCategory category,
									       HttpInstrumentationTestType type)
		{
			var certificateProvider = DependencyInjector.Get<ICertificateProvider> ();
			var acceptAll = certificateProvider.AcceptAll ();

			var name = GetTestName (category, type);

			var parameters = new HttpInstrumentationTestParameters (category, type, name, ResourceManager.SelfSignedServerCertificate) {
				ClientCertificateValidator = acceptAll
			};

			switch (GetEffectiveType (type)) {
			case HttpInstrumentationTestType.SimpleQueuedRequest:
			case HttpInstrumentationTestType.CancelQueuedRequest:
			case HttpInstrumentationTestType.CancelMainWhileQueued:
			case HttpInstrumentationTestType.NtlmWhileQueued:
				parameters.ConnectionLimit = 1;
				break;
			case HttpInstrumentationTestType.ThreeParallelRequests:
				parameters.ConnectionLimit = 5;
				break;
			case HttpInstrumentationTestType.ParallelRequestsSomeQueued:
				parameters.CountParallelRequests = 5;
				parameters.ConnectionLimit = 3;
				break;
			case HttpInstrumentationTestType.ManyParallelRequests:
				parameters.CountParallelRequests = 10;
				parameters.ConnectionLimit = 5;
				break;
			case HttpInstrumentationTestType.ManyParallelRequestsStress:
				parameters.CountParallelRequests = 100;
				parameters.ConnectionLimit = 25;
				break;
			case HttpInstrumentationTestType.CloseIdleConnection:
				parameters.IdleTime = 750;
				break;
			}

			return parameters;
		}

		public async Task Run (TestContext ctx, CancellationToken cancellationToken)
		{
			var me = $"{ME}.{nameof (Run)}()";

			var handler = CreateHandler (ctx, true);

			HttpStatusCode expectedStatus;
			WebExceptionStatus expectedError;

			switch (EffectiveType) {
			case HttpInstrumentationTestType.InvalidDataDuringHandshake:
				expectedStatus = HttpStatusCode.InternalServerError;
				expectedError = WebExceptionStatus.SecureChannelFailure;
				break;
			case HttpInstrumentationTestType.AbortDuringHandshake:
			case HttpInstrumentationTestType.CancelMainWhileQueued:
			case HttpInstrumentationTestType.NtlmWhileQueued:
				expectedStatus = HttpStatusCode.InternalServerError;
				expectedError = WebExceptionStatus.RequestCanceled;
				break;
			case HttpInstrumentationTestType.Get404:
				expectedStatus = HttpStatusCode.NotFound;
				expectedError = WebExceptionStatus.ProtocolError;
				break;

			case HttpInstrumentationTestType.CloseRequestStream:
				expectedStatus = HttpStatusCode.InternalServerError;
				expectedError = WebExceptionStatus.RequestCanceled;
				break;

			default:
				expectedStatus = HttpStatusCode.OK;
				expectedError = WebExceptionStatus.Success;
				break;
			}

			ctx.LogDebug (2, $"{me}");

			currentOperation = new Operation (this, handler, false, expectedStatus, expectedError);
			currentOperation.Start (ctx, cancellationToken);

			try {
				await currentOperation.WaitForCompletion ().ConfigureAwait (false);
				ctx.LogDebug (2, $"{me} operation done");
			} catch (Exception ex) {
				ctx.LogDebug (2, $"{me} operation failed: {ex.Message}");
				throw;
			}

			Operation secondOperation = null;

			switch (EffectiveType) {
			case HttpInstrumentationTestType.ParallelRequests:
				ctx.Assert (readHandlerCalled, Is.EqualTo (2), "ReadHandler called twice");
				break;
			case HttpInstrumentationTestType.ThreeParallelRequests:
				ctx.Assert (readHandlerCalled, Is.EqualTo (3), "ReadHandler called three times");
				break;
			case HttpInstrumentationTestType.SimpleQueuedRequest:
				ctx.Assert (queuedOperation, Is.Not.Null, "have queued task");
				await queuedOperation.WaitForCompletion ().ConfigureAwait (false);
				ctx.Assert (readHandlerCalled, Is.EqualTo (2), "ReadHandler called twice");
				break;
			case HttpInstrumentationTestType.ParallelRequestsSomeQueued:
			case HttpInstrumentationTestType.ManyParallelRequests:
			case HttpInstrumentationTestType.ManyParallelRequestsStress:
				// ctx.Assert (readHandlerCalled, Is.EqualTo (Parameters.CountParallelRequests + 1), "ReadHandler count");
				break;
			case HttpInstrumentationTestType.ReuseConnection:
			case HttpInstrumentationTestType.ReuseConnection2:
			case HttpInstrumentationTestType.ReuseAfterPartialRead:
			case HttpInstrumentationTestType.CustomConnectionGroup:
			case HttpInstrumentationTestType.ReuseCustomConnectionGroup:
				secondOperation = StartSecond (ctx, cancellationToken, CreateHandler (ctx, false));
				break;
			case HttpInstrumentationTestType.CloseIdleConnection:
				ctx.LogDebug (5, $"{me}: active connections: {currentOperation.ServicePoint.CurrentConnections}");
				await Task.Delay ((int)(Parameters.IdleTime * 2.5)).ConfigureAwait (false);
				ctx.LogDebug (5, $"{me}: active connections #1: {currentOperation.ServicePoint.CurrentConnections}");
				ctx.Assert (currentOperation.ServicePoint.CurrentConnections, Is.EqualTo (0), "current connections");
				break;
			case HttpInstrumentationTestType.CloseCustomConnectionGroup:
				ctx.LogDebug (5, $"{me}: active connections: {currentOperation.ServicePoint.CurrentConnections}");
				currentOperation.ServicePoint.CloseConnectionGroup (currentOperation.Request.RequestExt.ConnectionGroupName);
				ctx.LogDebug (5, $"{me}: active connections #1: {currentOperation.ServicePoint.CurrentConnections}");
				break;
			}

			if (secondOperation != null) {
				ctx.LogDebug (2, $"{me} waiting for second operation.");
				try {
					await secondOperation.WaitForCompletion ().ConfigureAwait (false);
					ctx.LogDebug (2, $"{me} done waiting for second operation.");
				} catch (Exception ex) {
					ctx.LogDebug (2, $"{me} waiting for second operation failed: {ex.Message}.");
					throw;
				}
			}

			if (queuedOperation != null) {
				ctx.LogDebug (2, $"{me} waiting for queued operations.");
				try {
					await queuedOperation.WaitForCompletion ().ConfigureAwait (false);
					ctx.LogDebug (2, $"{me} done waiting for queued operations.");
				} catch (Exception ex) {
					ctx.LogDebug (2, $"{me} waiting for queued operations failed: {ex.Message}.");
					throw;
				}
			}

			Server.CloseAll ();
		}

		AuthenticationManager GetAuthenticationManager ()
		{
			var manager = new AuthenticationManager (AuthenticationType.NTLM, AuthenticationHandler.GetCredentials ());
			var old = Interlocked.CompareExchange (ref authManager, manager, null);
			return old ?? manager;
		}

		Handler CreateHandler (TestContext ctx, bool primary)
		{
			var hello = new HelloWorldHandler (EffectiveType.ToString ());
			var helloKeepAlive = new HelloWorldHandler (EffectiveType.ToString ()) {
				Flags = RequestFlags.KeepAlive
			};
			var postHello = new PostHandler (EffectiveType.ToString (), HttpContent.HelloWorld);
			var chunkedPost = new PostHandler (EffectiveType.ToString (), HttpContent.HelloChunked, TransferMode.Chunked);

			switch (EffectiveType) {
			case HttpInstrumentationTestType.SimpleNtlm:
			case HttpInstrumentationTestType.NtlmWhileQueued:
				return new AuthenticationHandler (AuthenticationType.NTLM, hello);
			case HttpInstrumentationTestType.ReuseConnection:
				return new HttpInstrumentationHandler (this, null, null, !primary);
			case HttpInstrumentationTestType.ReuseAfterPartialRead:
				return new HttpInstrumentationHandler (this, null, ConnectionHandler.GetLargeStringContent (250), !primary);
			case HttpInstrumentationTestType.ReuseConnection2:
				if (primary)
					return new HttpInstrumentationHandler (this, null, HttpContent.HelloWorld, false);
				return new HttpInstrumentationHandler (this, null, HttpContent.HelloWorld, true);
			case HttpInstrumentationTestType.SimplePost:
				return postHello;
			case HttpInstrumentationTestType.SimpleRedirect:
				return new RedirectHandler (hello, HttpStatusCode.Redirect);
			case HttpInstrumentationTestType.PostNtlm:
				return new AuthenticationHandler (AuthenticationType.NTLM, postHello);
			case HttpInstrumentationTestType.PostRedirect:
				return new RedirectHandler (postHello, HttpStatusCode.TemporaryRedirect);
			case HttpInstrumentationTestType.NtlmChunked:
				return new AuthenticationHandler (AuthenticationType.NTLM, chunkedPost);
			case HttpInstrumentationTestType.Get404:
				return new GetHandler (EffectiveType.ToString (), null, HttpStatusCode.NotFound);
			case HttpInstrumentationTestType.CloseIdleConnection:
			case HttpInstrumentationTestType.CloseCustomConnectionGroup:
				return new HttpInstrumentationHandler (this, null, null, false);
			case HttpInstrumentationTestType.NtlmInstrumentation:
			case HttpInstrumentationTestType.NtlmClosesConnection:
			case HttpInstrumentationTestType.ParallelNtlm:
				return new HttpInstrumentationHandler (this, GetAuthenticationManager (), null, true);
			case HttpInstrumentationTestType.LargeHeader:
			case HttpInstrumentationTestType.LargeHeader2:
			case HttpInstrumentationTestType.SendResponseAsBlob:
				return new HttpInstrumentationHandler (this, null, ConnectionHandler.TheQuickBrownFoxContent, true);
			case HttpInstrumentationTestType.CustomConnectionGroup:
			case HttpInstrumentationTestType.ReuseCustomConnectionGroup:
			case HttpInstrumentationTestType.CloseRequestStream:
			case HttpInstrumentationTestType.ReadTimeout:
				return new HttpInstrumentationHandler (this, null, null, !primary);
			default:
				return hello;
			}
		}

		async Task HandleRequest (
			TestContext ctx, HttpInstrumentationHandler handler,
			HttpConnection connection, HttpRequest request,
			AuthenticationState state, CancellationToken cancellationToken)
		{
			switch (EffectiveType) {
			case HttpInstrumentationTestType.ReuseConnection:
			case HttpInstrumentationTestType.ReuseCustomConnectionGroup:
				MustReuseConnection ();
				break;

			case HttpInstrumentationTestType.ParallelNtlm:
				await ParallelNtlm ().ConfigureAwait (false);
				break;

			case HttpInstrumentationTestType.ReuseAfterPartialRead:
				// We can't reuse the connection because we did not read the entire response.
				MustNotReuseConnection ();
				break;

			case HttpInstrumentationTestType.CustomConnectionGroup:
				// We can't reuse the connection because we're in a different connection group.
				MustNotReuseConnection ();
				break;
			}

			async Task ParallelNtlm ()
			{
				var firstHandler = (HttpInstrumentationHandler)currentOperation.Handler;
				ctx.LogDebug (2, $"{handler.Message}: TEST {state} {handler == firstHandler} {handler.RemoteEndPoint}");
				if (handler != firstHandler || state != AuthenticationState.Challenge)
					return;

				var newHandler = (HttpInstrumentationHandler)firstHandler.Clone ();
				var operation = await StartParallel (ctx, cancellationToken, newHandler).ConfigureAwait (false);
				if (Interlocked.CompareExchange (ref queuedOperation, operation, null) != null)
					throw ctx.AssertFail ("Invalid nested call");
				await operation.WaitForRequest ();
				// await operation.WaitForCompletion (false).ConfigureAwait (false);
			}

			void MustNotReuseConnection ()
			{
				var firstHandler = (HttpInstrumentationHandler)currentOperation.Handler;
				ctx.LogDebug (2, $"{handler.Message}: {handler == firstHandler} {handler.RemoteEndPoint}");
				if (handler == firstHandler)
					return;
				ctx.Assert (connection.RemoteEndPoint, Is.Not.EqualTo (firstHandler.RemoteEndPoint), "RemoteEndPoint");
			}

			void MustReuseConnection ()
			{
				var firstHandler = (HttpInstrumentationHandler)currentOperation.Handler;
				ctx.LogDebug (2, $"{handler.Message}: {handler == firstHandler} {handler.RemoteEndPoint}");
				if (handler == firstHandler)
					return;
				ctx.Assert (connection.RemoteEndPoint, Is.EqualTo (firstHandler.RemoteEndPoint), "RemoteEndPoint");
			}
		}

		async Task<Operation> StartParallel (TestContext ctx, CancellationToken cancellationToken, Handler handler,
						     HttpStatusCode expectedStatus = HttpStatusCode.OK,
						     WebExceptionStatus expectedError = WebExceptionStatus.Success)
		{
			await Server.StartParallel (ctx, cancellationToken).ConfigureAwait (false);
			var operation = new Operation (this, handler, true, expectedStatus, expectedError);
			operation.Start (ctx, cancellationToken);
			return operation;
		}

		async Task RunParallel (TestContext ctx, CancellationToken cancellationToken, Handler handler,
					HttpStatusCode expectedStatus = HttpStatusCode.OK,
					WebExceptionStatus expectedError = WebExceptionStatus.Success)
		{
			var operation = await StartParallel (ctx, cancellationToken, handler, expectedStatus, expectedError).ConfigureAwait (false);
			await operation.WaitForCompletion ();
		}

		Operation StartSecond (TestContext ctx, CancellationToken cancellationToken, Handler handler,
				       HttpStatusCode expectedStatus = HttpStatusCode.OK,
				       WebExceptionStatus expectedError = WebExceptionStatus.Success)
		{
			var operation = new Operation (this, handler, true, expectedStatus, expectedError);
			operation.Start (ctx, cancellationToken);
			return operation;
		}

		protected override async Task Initialize (TestContext ctx, CancellationToken cancellationToken)
		{
			await Server.Initialize (ctx, cancellationToken).ConfigureAwait (false);
		}

		protected override async Task Destroy (TestContext ctx, CancellationToken cancellationToken)
		{
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

		class Operation : TraditionalTestRunner
		{
			public HttpInstrumentationTestRunner Parent {
				get;
			}

			public bool IsParallelRequest {
				get;
			}

			public HttpStatusCode ExpectedStatus {
				get;
			}

			public WebExceptionStatus ExpectedError {
				get;
			}

			TraditionalRequest currentRequest;
			ServicePoint servicePoint;
			TaskCompletionSource<TraditionalRequest> requestTask;
			TaskCompletionSource<bool> requestDoneTask;
			Task runTask;

			public bool HasRequest => currentRequest != null;

			public TraditionalRequest Request {
				get {
					if (currentRequest == null)
						throw new InvalidOperationException ();
					return currentRequest;
				}
			}

			public ServicePoint ServicePoint {
				get {
					if (servicePoint == null)
						throw new InvalidOperationException ();
					return servicePoint;
				}
			}

			public Operation (HttpInstrumentationTestRunner parent, Handler handler,
					  bool parallel, HttpStatusCode expectedStatus, WebExceptionStatus expectedError)
				: base (parent.Server, handler, true)
			{
				Parent = parent;
				IsParallelRequest = parallel;
				ExpectedStatus = expectedStatus;
				ExpectedError = expectedError;
				requestTask = new TaskCompletionSource<TraditionalRequest> ();
				requestDoneTask = new TaskCompletionSource<bool> ();
			}

			protected override Request CreateRequest (TestContext ctx, Uri uri)
			{
				switch (Parent.EffectiveType) {
				case HttpInstrumentationTestType.ReuseAfterPartialRead:
				case HttpInstrumentationTestType.CloseRequestStream:
				case HttpInstrumentationTestType.ReadTimeout:
					return new HttpInstrumentationRequest (Parent, uri);
				default:
					return base.CreateRequest (ctx, uri);
				}
			}

			protected override void ConfigureRequest (TestContext ctx, Uri uri, Request request)
			{
				currentRequest = (TraditionalRequest)request;
				servicePoint = currentRequest.RequestExt.ServicePoint;
				requestTask.SetResult (currentRequest);

				if (IsParallelRequest)
					ConfigureParallelRequest (ctx);
				else
					ConfigureRequest (ctx);

				base.ConfigureRequest (ctx, uri, request);
			}

			public void ConfigureParallelRequest (TestContext ctx)
			{
				switch (Parent.EffectiveType) {
				case HttpInstrumentationTestType.ParallelRequests:
				case HttpInstrumentationTestType.SimpleQueuedRequest:
				case HttpInstrumentationTestType.CancelQueuedRequest:
				case HttpInstrumentationTestType.CancelMainWhileQueued:
				case HttpInstrumentationTestType.NtlmWhileQueued:
					ctx.Assert (servicePoint, Is.Not.Null, "ServicePoint");
					ctx.Assert (servicePoint.CurrentConnections, Is.EqualTo (1), "ServicePoint.CurrentConnections");
					break;
				case HttpInstrumentationTestType.ThreeParallelRequests:
				case HttpInstrumentationTestType.ParallelRequestsSomeQueued:
				case HttpInstrumentationTestType.ManyParallelRequests:
				case HttpInstrumentationTestType.ManyParallelRequestsStress:
				case HttpInstrumentationTestType.ReuseConnection:
				case HttpInstrumentationTestType.ReuseConnection2:
				case HttpInstrumentationTestType.ReuseAfterPartialRead:
				case HttpInstrumentationTestType.ParallelNtlm:
				case HttpInstrumentationTestType.CustomConnectionGroup:
					break;
				case HttpInstrumentationTestType.ReuseCustomConnectionGroup:
					currentRequest.RequestExt.ConnectionGroupName = "custom";
					break;
				default:
					throw ctx.AssertFail (Parent.EffectiveType);
				}
			}

			public void ConfigureRequest (TestContext ctx)
			{
				if (Parent.Parameters.ConnectionLimit != 0)
					ServicePoint.ConnectionLimit = Parent.Parameters.ConnectionLimit;
				if (Parent.Parameters.IdleTime != 0)
					ServicePoint.MaxIdleTime = Parent.Parameters.IdleTime;
				currentRequest.RequestExt.ReadWriteTimeout = int.MaxValue;
				currentRequest.RequestExt.Timeout = int.MaxValue;

				switch (Parent.EffectiveType) {
				case HttpInstrumentationTestType.SimplePost:
					currentRequest.SetContentLength (((PostHandler)Handler).Content.Length);
					break;
				case HttpInstrumentationTestType.CustomConnectionGroup:
				case HttpInstrumentationTestType.ReuseCustomConnectionGroup:
					currentRequest.RequestExt.ConnectionGroupName = "custom";
					break;
				}
			}

			public void Start (TestContext ctx, CancellationToken cancellationToken)
			{
				runTask = Run (ctx, cancellationToken, ExpectedStatus, ExpectedError).ContinueWith (t => {
					if (t.IsFaulted)
						requestDoneTask.TrySetException (t.Exception);
					else if (t.IsCanceled)
						requestDoneTask.TrySetCanceled ();
					else
						requestDoneTask.TrySetResult (true);
				});
			}

			public Task<TraditionalRequest> WaitForRequest ()
			{
				return requestTask.Task;
			}

			public async Task<bool> WaitForCompletion (bool ignoreErrors = false)
			{
				try {
					await requestDoneTask.Task.ConfigureAwait (false);
					return true;
				} catch {
					if (ignoreErrors)
						return false;
					throw;
				}
			}
		}

		StreamInstrumentation serverInstrumentation;
		Operation currentOperation;
		Operation queuedOperation;
		AuthenticationManager authManager;
		int readHandlerCalled;

		async Task<bool> IHttpServerDelegate.CheckCreateConnection (
			TestContext ctx, HttpConnection connection, Task initTask,
			CancellationToken cancellationToken)
		{
			try {
				await initTask.ConfigureAwait (false);
				return true;
			} catch (OperationCanceledException) {
				return false;
			} catch {
				if (EffectiveType == HttpInstrumentationTestType.InvalidDataDuringHandshake ||
				    EffectiveType == HttpInstrumentationTestType.AbortDuringHandshake ||
				    EffectiveType == HttpInstrumentationTestType.CancelMainWhileQueued)
					return false;
				throw;
			}
		}

		Task<bool> IHttpServerDelegate.HandleConnection (TestContext ctx, HttpConnection connection, CancellationToken cancellationToken)
		{
			if (EffectiveType == HttpInstrumentationTestType.CloseRequestStream) {
				return Task.FromResult (false);
			}

			return Task.FromResult (true);
		}

		bool IHttpServerDelegate.HandleConnection (TestContext ctx, HttpConnection connection, HttpRequest request, Handler handler)
		{
			return true;
		}

		Stream IHttpServerDelegate.CreateNetworkStream (TestContext ctx, Socket socket, bool ownsSocket)
		{
			var me = $"{Parameters.Identifier}:{serverInstrumentation == null}:{socket.RemoteEndPoint}";
			var instrumentation = new StreamInstrumentation (ctx, me, socket, ownsSocket);
			var old = Interlocked.CompareExchange (ref serverInstrumentation, instrumentation, null);
			InstallReadHandler (ctx, old == null, instrumentation);
			return instrumentation;
		}

		async Task<bool> IHttpInstrumentation.WriteResponse (TestContext ctx, HttpConnection connection,
								     HttpResponse response, CancellationToken cancellationToken)
		{
			await connection.WriteResponse (ctx, response, cancellationToken).ConfigureAwait (false);
			return true;
		}

		async Task IHttpInstrumentation.ResponseHeadersWritten (TestContext ctx, CancellationToken cancellationToken)
		{
			if (EffectiveType != HttpInstrumentationTestType.NtlmWhileQueued)
				return;

			await Task.Delay (500);
			currentOperation.Request.Request.Abort ();
		}

		void InstallReadHandler (TestContext ctx, bool primary, StreamInstrumentation instrumentation)
		{
			instrumentation.OnNextRead ((b, o, s, f, c) => ReadHandler (ctx, primary, instrumentation, b, o, s, f, c));
		}

		async Task<int> ReadHandler (TestContext ctx, bool primary,
					     StreamInstrumentation instrumentation,
					     byte[] buffer, int offset, int size,
					     StreamInstrumentation.AsyncReadFunc func,
					     CancellationToken cancellationToken)
		{
			cancellationToken.ThrowIfCancellationRequested ();

			var ret = await func (buffer, offset, size, cancellationToken).ConfigureAwait (false);

			Interlocked.Increment (ref readHandlerCalled);

			switch (EffectiveType) {
			case HttpInstrumentationTestType.Simple:
			case HttpInstrumentationTestType.SimplePost:
			case HttpInstrumentationTestType.Get404:
			case HttpInstrumentationTestType.CloseIdleConnection:
			case HttpInstrumentationTestType.CloseCustomConnectionGroup:
			case HttpInstrumentationTestType.LargeHeader:
			case HttpInstrumentationTestType.LargeHeader2:
			case HttpInstrumentationTestType.SendResponseAsBlob:
			case HttpInstrumentationTestType.CloseRequestStream:
			case HttpInstrumentationTestType.ReadTimeout:
				ctx.Assert (primary, "Primary request");
				break;

			case HttpInstrumentationTestType.ParallelRequests:
				ctx.Assert (currentOperation.HasRequest, "current request");
				if (primary) {
					await RunParallel (ctx, cancellationToken, HelloWorldHandler.GetSimple ()).ConfigureAwait (false);
				} else {
					ctx.Assert (currentOperation.ServicePoint.CurrentConnections, Is.EqualTo (2), "ServicePoint.CurrentConnections");
				}
				break;

			case HttpInstrumentationTestType.SimpleQueuedRequest:
				ctx.Assert (currentOperation.HasRequest, "current request");
				if (primary) {
					var operation = await StartParallel (ctx, cancellationToken, HelloWorldHandler.GetSimple ()).ConfigureAwait (false);
					if (Interlocked.CompareExchange (ref queuedOperation, operation, null) != null)
						throw ctx.AssertFail ("Invalid nested call");
				}
				break;

			case HttpInstrumentationTestType.ThreeParallelRequests:
				ctx.Assert (currentOperation.HasRequest, "current request");
				if (primary) {
					var secondTask = RunParallel (ctx, cancellationToken, HelloWorldHandler.GetSimple ());
					var thirdTask = RunParallel (ctx, cancellationToken, HelloWorldHandler.GetSimple ());
					await Task.WhenAll (secondTask, thirdTask).ConfigureAwait (false);
				} else {
					// ctx.Assert (currentOperation.ServicePoint.CurrentConnections, Is.EqualTo (3), "ServicePoint.CurrentConnections");
				}
				break;

			case HttpInstrumentationTestType.ParallelRequestsSomeQueued:
			case HttpInstrumentationTestType.ManyParallelRequests:
			case HttpInstrumentationTestType.ManyParallelRequestsStress:
				ctx.Assert (currentOperation.HasRequest, "current request");
				if (primary) {
					var parallelTasks = new Task[Parameters.CountParallelRequests];
					for (int i = 0; i < parallelTasks.Length; i++)
						parallelTasks[i] = RunParallel (ctx, cancellationToken, HelloWorldHandler.GetSimple ());
					await Task.WhenAll (parallelTasks).ConfigureAwait (false);
				} else {
					// ctx.Expect (currentServicePoint.CurrentConnections, Is.EqualTo (3), "ServicePoint.CurrentConnections");
				}
				break;

			case HttpInstrumentationTestType.AbortDuringHandshake:
				ctx.Assert (primary, "Primary request");
				ctx.Assert (currentOperation.HasRequest, "current request");
				currentOperation.Request.Request.Abort ();
				// Wait until the client request finished, to make sure we are actually aboring.
				await currentOperation.WaitForCompletion ().ConfigureAwait (false);
				break;

			case HttpInstrumentationTestType.InvalidDataDuringHandshake:
				ctx.Assert (primary, "Primary request");
				InstallReadHandler (ctx, primary, instrumentation);
				if (ret > 50) {
					for (int i = 10; i < 40; i++)
						buffer[i] = 0xAA;
				}
				break;

			case HttpInstrumentationTestType.CancelQueuedRequest:
				ctx.Assert (currentOperation.HasRequest, "current request");
				if (primary) {
					var operation = await StartParallel (
						ctx, cancellationToken, HelloWorldHandler.GetSimple (),
						HttpStatusCode.InternalServerError, WebExceptionStatus.RequestCanceled).ConfigureAwait (false);
					if (Interlocked.CompareExchange (ref queuedOperation, operation, null) != null)
						throw new InvalidOperationException ("Invalid nested call.");
					var request = await operation.WaitForRequest ().ConfigureAwait (false);
					// Wait a bit to make sure the request has been queued.
					await Task.Delay (500).ConfigureAwait (false);
					request.Request.Abort ();
				}
				break;

			case HttpInstrumentationTestType.CancelMainWhileQueued:
				ctx.Assert (currentOperation.HasRequest, "current request");
				if (primary) {
					var operation = await StartParallel (
						ctx, cancellationToken, HelloWorldHandler.GetSimple ()).ConfigureAwait (false);
					if (Interlocked.CompareExchange (ref queuedOperation, operation, null) != null)
						throw new InvalidOperationException ("Invalid nested call.");
					var request = await operation.WaitForRequest ().ConfigureAwait (false);
					// Wait a bit to make sure the request has been queued.
					await Task.Delay (2500).ConfigureAwait (false);
					instrumentation.Dispose ();
					currentOperation.Request.Request.Abort ();
				}
				break;

			case HttpInstrumentationTestType.SimpleNtlm:
			case HttpInstrumentationTestType.PostNtlm:
			case HttpInstrumentationTestType.SimpleRedirect:
			case HttpInstrumentationTestType.PostRedirect:
			case HttpInstrumentationTestType.NtlmChunked:
			case HttpInstrumentationTestType.NtlmInstrumentation:
			case HttpInstrumentationTestType.NtlmClosesConnection:
				break;

			case HttpInstrumentationTestType.NtlmWhileQueued:
				ctx.Assert (currentOperation.HasRequest, "current request");
				if (primary) {
					var operation = await StartParallel (ctx, cancellationToken, HelloWorldHandler.GetSimple ()).ConfigureAwait (false);
					if (Interlocked.CompareExchange (ref queuedOperation, operation, null) != null)
						throw ctx.AssertFail ("Invalid nested call");
				}
				break;

			case HttpInstrumentationTestType.ReuseConnection:
			case HttpInstrumentationTestType.ReuseConnection2:
			case HttpInstrumentationTestType.ReuseAfterPartialRead:
			case HttpInstrumentationTestType.CustomConnectionGroup:
			case HttpInstrumentationTestType.ReuseCustomConnectionGroup:
				break;

			case HttpInstrumentationTestType.ParallelNtlm:
				if (false && primary) {
					var secondaryHandler = new HttpInstrumentationHandler (this, GetAuthenticationManager (), null, false);
					var operation = await StartParallel (ctx, cancellationToken, secondaryHandler).ConfigureAwait (false);
					if (Interlocked.CompareExchange (ref queuedOperation, operation, null) != null)
						throw ctx.AssertFail ("Invalid nested call");
				}
				break;

			default:
				throw ctx.AssertFail (EffectiveType);
			}

			return ret;
		}

		class HttpInstrumentationRequest : TraditionalRequest
		{
			public HttpInstrumentationTestRunner TestRunner {
				get;
			}

			TaskCompletionSource<bool> finishedTcs;

			public Task WaitForCompletion ()
			{
				return finishedTcs.Task;
			}

			public HttpInstrumentationRequest (HttpInstrumentationTestRunner runner, Uri uri)
				: base (uri)
			{
				TestRunner = runner;
				finishedTcs = new TaskCompletionSource<bool> (); 
			}

			public override async Task<Response> SendAsync (TestContext ctx, CancellationToken cancellationToken)
			{
				var portable = DependencyInjector.Get<IPortableSupport> ();
				if (TestRunner.EffectiveType == HttpInstrumentationTestType.CloseRequestStream) {
					Request.Method = "POST";
					RequestExt.SetContentLength (16384);
					var stream = await RequestExt.GetRequestStreamAsync ().ConfigureAwait (false);
					try {
						portable.Close (stream);
						throw ctx.AssertFail ("Expected exception.");
					} catch (Exception ex) {
						return new SimpleResponse (this, HttpStatusCode.InternalServerError, null, ex);
					}
				}

				return await base.SendAsync (ctx, cancellationToken).ConfigureAwait (false);
			}

			protected override async Task<Response> GetResponseFromHttp (
				TestContext ctx, HttpWebResponse response, WebException error, CancellationToken cancellationToken)
			{
				cancellationToken.ThrowIfCancellationRequested ();
				HttpContent content = null;

				switch (TestRunner.EffectiveType) {
				case HttpInstrumentationTestType.ReuseAfterPartialRead:
					content = await ReadStringAsBuffer (1234).ConfigureAwait (false);
					break;

				case HttpInstrumentationTestType.ReadTimeout:
					return await ReadWithTimeout (1500).ConfigureAwait (false);

				default:
					content = await ReadAsString ().ConfigureAwait (false);
					break;
				}

				response.Dispose ();
				finishedTcs.TrySetResult (true);
				return new SimpleResponse (this, response.StatusCode, content, error);

				async Task<Response> ReadWithTimeout (int timeout)
				{
					using (var reader = new StreamReader (response.GetResponseStream ())) {
						var timeoutTask = Task.Delay (timeout);
						var readTask = reader.ReadToEndAsync ();
						var ret = await Task.WhenAny (timeoutTask, readTask).ConfigureAwait (false);
						finishedTcs.TrySetResult (true);
						if (ret == timeoutTask)
							throw ctx.AssertFail ("Timeout expired.");
						await ctx.AssertException<WebException> (() => readTask, "Expecting exception");
						return new SimpleResponse (this, HttpStatusCode.OK, null, null);
					}
				}

				async Task<HttpContent> ReadStringAsBuffer (int size)
				{
					using (var stream = response.GetResponseStream ()) {
						var buffer = new byte[size];
						var ret = await stream.ReadAsync (buffer, 0, buffer.Length).ConfigureAwait (false);
						ctx.Assert (ret, Is.EqualTo (buffer.Length));
						return StringContent.CreateMaybeNull (new ASCIIEncoding ().GetString (buffer, 0, ret));
					}
				}

				async Task<HttpContent> ReadAsString ()
				{
					using (var reader = new StreamReader (response.GetResponseStream ())) {
						string text = null;
						if (!reader.EndOfStream)
							text = await reader.ReadToEndAsync ().ConfigureAwait (false);
						return StringContent.CreateMaybeNull (text);
					}
				}
			}
		}

		class HttpInstrumentationContent : HttpContent
		{
			public HttpInstrumentationTestRunner TestRunner {
				get;
			}

			public HttpInstrumentationRequest Request {
				get;
			}

			public HttpInstrumentationContent (HttpInstrumentationTestRunner runner, HttpInstrumentationRequest request)
			{
				TestRunner = runner;
				Request = request;
			}

			public override bool HasLength => true;

			public override int Length => 4096;

			public override void AddHeadersTo (HttpMessage message)
			{
				message.ContentLength = Length;
				message.ContentType = "text/plain";
			}

			public override byte[] AsByteArray ()
			{
				throw new NotImplementedException ();
			}

			public override string AsString ()
			{
				throw new NotImplementedException ();
			}

			public override async Task WriteToAsync (TestContext ctx, StreamWriter writer)
			{
				await writer.WriteAsync (ConnectionHandler.TheQuickBrownFox).ConfigureAwait (false);
				await writer.FlushAsync ();
				await Task.WhenAny (Request.WaitForCompletion (), Task.Delay (10000));
			}
		}

		class HttpInstrumentationHandler : Handler
		{
			public HttpInstrumentationTestRunner TestRunner {
				get;
			}

			public string Message {
				get;
			}

			public bool CloseConnection {
				get;
			}

			public HttpContent Content {
				get;
			}

			public IPEndPoint RemoteEndPoint {
				get;
				private set;
			}

			public Handler Target {
				get;
			}

			public AuthenticationManager AuthManager {
				get;
			}

			public HttpInstrumentationHandler (HttpInstrumentationTestRunner parent, AuthenticationManager authManager,
							   HttpContent content, bool closeConnection)
				: base (parent.EffectiveType.ToString ())
			{
				TestRunner = parent;
				AuthManager = authManager;
				Content = content;
				CloseConnection = closeConnection;
				Message = $"{GetType ().Name}({parent.EffectiveType})";
				Flags = RequestFlags.KeepAlive;
				if (closeConnection)
					Flags |= RequestFlags.CloseConnection;

				if (AuthManager != null)
					Target = new HelloWorldHandler (Message);
			}

			HttpInstrumentationHandler (HttpInstrumentationHandler other)
				: base (other.Value)
			{
				TestRunner = other.TestRunner;
				Content = other.Content;
				CloseConnection = CloseConnection;
				Message = other.Message;
				Flags = other.Flags;
				Target = other.Target;
				AuthManager = other.AuthManager;
			}

			HttpInstrumentationRequest currentRequest;

			public override object Clone ()
			{
				return new HttpInstrumentationHandler (this);
			}

			public override void ConfigureRequest (Request request, Uri uri)
			{
				if (AuthManager != null)
					AuthManager.ConfigureRequest (request);

				if (request is HttpInstrumentationRequest instrumentationRequest) {
					if (Interlocked.CompareExchange (ref currentRequest, instrumentationRequest, null) != null)
						throw new InvalidOperationException ();
				}

				switch (TestRunner.EffectiveType) {
				case HttpInstrumentationTestType.ReuseConnection2:
					request.Method = "POST";

					if (Content != null) {
						request.SetContentType ("text/plain");
						request.Content = Content.RemoveTransferEncoding ();
					}
					break;

				case HttpInstrumentationTestType.NtlmInstrumentation:
				case HttpInstrumentationTestType.NtlmClosesConnection:
				case HttpInstrumentationTestType.ParallelNtlm:
					break;

				case HttpInstrumentationTestType.LargeHeader:
				case HttpInstrumentationTestType.LargeHeader2:
				case HttpInstrumentationTestType.SendResponseAsBlob:
					break;

				case HttpInstrumentationTestType.ReadTimeout:
					currentRequest.RequestExt.ReadWriteTimeout = 100;
					break;
				}

				base.ConfigureRequest (request, uri);
			}

			async Task<HttpResponse> HandleNtlmRequest (
				TestContext ctx, HttpConnection connection, HttpRequest request,
				RequestFlags effectiveFlags, CancellationToken cancellationToken)
			{
				var me = $"{Message}.{nameof (HandleNtlmRequest)}";
				ctx.LogDebug (3, $"{me}: {connection.RemoteEndPoint}");

				AuthenticationState state;
				var response = AuthManager.HandleAuthentication (ctx, connection, request, out state);
				ctx.LogDebug (3, $"{me}: {connection.RemoteEndPoint} - {state} {response}");

				await TestRunner.HandleRequest (
					ctx, this, connection, request, state, cancellationToken).ConfigureAwait (false);

				if (response != null) {
					connection.Server.RegisterHandler (ctx, request.Path, this);
					return response;
				}

				cancellationToken.ThrowIfCancellationRequested ();
				return await Target.HandleRequest (ctx, connection, request, effectiveFlags, cancellationToken);
			}

			protected internal override async Task<HttpResponse> HandleRequest (
				TestContext ctx, HttpConnection connection, HttpRequest request,
				RequestFlags effectiveFlags, CancellationToken cancellationToken)
			{
				RemoteEndPoint = connection.RemoteEndPoint;

				switch (TestRunner.EffectiveType) {
				case HttpInstrumentationTestType.ReuseConnection:
				case HttpInstrumentationTestType.CloseIdleConnection:
				case HttpInstrumentationTestType.CloseCustomConnectionGroup:
				case HttpInstrumentationTestType.LargeHeader:
				case HttpInstrumentationTestType.LargeHeader2:
				case HttpInstrumentationTestType.SendResponseAsBlob:
				case HttpInstrumentationTestType.ReuseAfterPartialRead:
				case HttpInstrumentationTestType.CustomConnectionGroup:
				case HttpInstrumentationTestType.ReuseCustomConnectionGroup:
				case HttpInstrumentationTestType.CloseRequestStream:
				case HttpInstrumentationTestType.ReadTimeout:
					ctx.Assert (request.Method, Is.EqualTo ("GET"), "method");
					break;

				case HttpInstrumentationTestType.ReuseConnection2:
					ctx.Assert (request.Method, Is.EqualTo ("POST"), "method");
					break;

				case HttpInstrumentationTestType.NtlmInstrumentation:
				case HttpInstrumentationTestType.NtlmClosesConnection:
				case HttpInstrumentationTestType.ParallelNtlm:
					return await HandleNtlmRequest (
						ctx, connection, request, effectiveFlags, cancellationToken).ConfigureAwait (false);

				default:
					throw ctx.AssertFail (TestRunner.EffectiveType);
				}

				await TestRunner.HandleRequest (
					ctx, this, connection, request, AuthenticationState.None, cancellationToken).ConfigureAwait (false);

				HttpResponse response;
				HttpInstrumentationContent content;

				switch (TestRunner.EffectiveType) {
				case HttpInstrumentationTestType.LargeHeader:
					response = new HttpResponse (HttpStatusCode.OK, Content);
					response.AddHeader ("LargeTest", ConnectionHandler.GetLargeTextBuffer (100));
					break;

				case HttpInstrumentationTestType.LargeHeader2:
					response = new HttpResponse (HttpStatusCode.OK, Content);
					response.AddHeader ("LargeTest", ConnectionHandler.GetLargeTextBuffer (100));
					response.WriteAsBlob = true;
					break;

				case HttpInstrumentationTestType.SendResponseAsBlob:
					response = new HttpResponse (HttpStatusCode.OK, Content);
					response.WriteAsBlob = true;
					break;

				case HttpInstrumentationTestType.ReuseAfterPartialRead:
					response = new HttpResponse (HttpStatusCode.OK, Content);
					response.WriteAsBlob = true;
					break;

				case HttpInstrumentationTestType.ReadTimeout:
					content = new HttpInstrumentationContent (TestRunner, currentRequest);
					response = new HttpResponse (HttpStatusCode.OK, content);
					break;

				default:
					response = HttpResponse.CreateSuccess (Message);
					break;
				}

				return response;
			}

			public override bool CheckResponse (TestContext ctx, Response response)
			{
				if (Target != null)
					return Target.CheckResponse (ctx, response);

				if (TestRunner.EffectiveType == HttpInstrumentationTestType.ReadTimeout)
					return ctx.Expect (response.Status, Is.EqualTo (HttpStatusCode.OK), "response.StatusCode");

				if (!ctx.Expect (response.Content, Is.Not.Null, "response.Content != null"))
					return false;

				if (TestRunner.EffectiveType == HttpInstrumentationTestType.ReuseAfterPartialRead) {
					return ctx.Expect (response.Content.Length, Is.GreaterThan (0), "response.Content.Length");
				}

				HttpContent expectedContent = Content ?? new StringContent (Message);
				return HttpContent.Compare (ctx, response.Content, expectedContent, false, "response.Content");
			}
		}
	}
}
