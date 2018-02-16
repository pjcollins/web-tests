//
// HttpRequestTestRunner.cs
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

	[HttpRequestTestRunner]
	public class HttpRequestTestRunner : InstrumentationTestRunner
	{
		public HttpRequestTestType Type {
			get;
		}

		public HttpRequestTestType EffectiveType => GetEffectiveType (Type);

		static HttpRequestTestType GetEffectiveType (HttpRequestTestType type)
		{
			if (type == HttpRequestTestType.MartinTest)
				return MartinTest;
			return type;
		}

		public HttpRequestTestRunner (HttpServerProvider provider, HttpRequestTestType type)
			: base (provider, type.ToString ())
		{
			Type = type;
		}

		const HttpRequestTestType MartinTest = HttpRequestTestType.Simple;

		static readonly (HttpRequestTestType type, HttpRequestTestFlags flags)[] TestRegistration = {
			(HttpRequestTestType.Simple, HttpRequestTestFlags.Working),
			(HttpRequestTestType.SimpleNtlm, HttpRequestTestFlags.Working),
			(HttpRequestTestType.ReuseConnection, HttpRequestTestFlags.Working),
			(HttpRequestTestType.SimplePost, HttpRequestTestFlags.Working),
			(HttpRequestTestType.SimpleRedirect, HttpRequestTestFlags.Working),
			(HttpRequestTestType.PostRedirect, HttpRequestTestFlags.Working),
			(HttpRequestTestType.PostNtlm, HttpRequestTestFlags.Working),
			(HttpRequestTestType.NtlmChunked, HttpRequestTestFlags.Working),
			(HttpRequestTestType.ReuseConnection2, HttpRequestTestFlags.Working),
			(HttpRequestTestType.Get404, HttpRequestTestFlags.Working),
			(HttpRequestTestType.CloseIdleConnection, HttpRequestTestFlags.NewWebStack),
			(HttpRequestTestType.NtlmInstrumentation, HttpRequestTestFlags.NewWebStack),
			(HttpRequestTestType.NtlmClosesConnection, HttpRequestTestFlags.NewWebStack),
			(HttpRequestTestType.NtlmReusesConnection, HttpRequestTestFlags.NewWebStack),
			(HttpRequestTestType.ParallelNtlm, HttpRequestTestFlags.NewWebStack),
			(HttpRequestTestType.LargeHeader, HttpRequestTestFlags.Working),
			(HttpRequestTestType.LargeHeader2, HttpRequestTestFlags.Working),
			(HttpRequestTestType.SendResponseAsBlob, HttpRequestTestFlags.Working),
			(HttpRequestTestType.ReuseAfterPartialRead, HttpRequestTestFlags.WorkingRequireSSL),
			(HttpRequestTestType.CustomConnectionGroup, HttpRequestTestFlags.Working),
			(HttpRequestTestType.ReuseCustomConnectionGroup, HttpRequestTestFlags.Working),
			(HttpRequestTestType.CloseCustomConnectionGroup, HttpRequestTestFlags.Working),
			(HttpRequestTestType.CloseRequestStream, HttpRequestTestFlags.Working),
			(HttpRequestTestType.ReadTimeout, HttpRequestTestFlags.NewWebStack),
			(HttpRequestTestType.RedirectOnSameConnection, HttpRequestTestFlags.Working),
			(HttpRequestTestType.RedirectNoReuse, HttpRequestTestFlags.Working),
			(HttpRequestTestType.RedirectNoLength, HttpRequestTestFlags.NewWebStack),
			(HttpRequestTestType.PutChunked, HttpRequestTestFlags.Working),
			(HttpRequestTestType.PutChunkDontCloseRequest, HttpRequestTestFlags.NewWebStack),
			(HttpRequestTestType.ServerAbortsRedirect, HttpRequestTestFlags.Unstable),
			(HttpRequestTestType.ServerAbortsPost, HttpRequestTestFlags.NewWebStack),
			(HttpRequestTestType.PostChunked, HttpRequestTestFlags.Working),
			(HttpRequestTestType.EntityTooBig, HttpRequestTestFlags.NewWebStack),
			(HttpRequestTestType.PostContentLength, HttpRequestTestFlags.Working),
			(HttpRequestTestType.ClientAbortsPost, HttpRequestTestFlags.NewWebStack),
			(HttpRequestTestType.GetChunked, HttpRequestTestFlags.Working),
			(HttpRequestTestType.SimpleGZip, HttpRequestTestFlags.Working),
			(HttpRequestTestType.TestResponseStream, HttpRequestTestFlags.Working),
			(HttpRequestTestType.LargeChunkRead, HttpRequestTestFlags.Working),
			(HttpRequestTestType.LargeGZipRead, HttpRequestTestFlags.GZip),
			(HttpRequestTestType.GZipWithLength, HttpRequestTestFlags.GZip),
			(HttpRequestTestType.ResponseStreamCheckLength2, HttpRequestTestFlags.GZip),
			(HttpRequestTestType.ResponseStreamCheckLength, HttpRequestTestFlags.GZip),
			(HttpRequestTestType.GetNoLength, HttpRequestTestFlags.Working),
			(HttpRequestTestType.ImplicitHost, HttpRequestTestFlags.Working),
			(HttpRequestTestType.CustomHost, HttpRequestTestFlags.Working),
			(HttpRequestTestType.CustomHostWithPort, HttpRequestTestFlags.Working),
			(HttpRequestTestType.CustomHostDefaultPort, HttpRequestTestFlags.Working),
		};

		public static IList<HttpRequestTestType> GetInstrumentationTypes (TestContext ctx, HttpServerTestCategory category)
		{
			if (category == HttpServerTestCategory.MartinTest)
				return new[] { MartinTest };

			var setup = DependencyInjector.Get<IConnectionFrameworkSetup> ();
			return TestRegistration.Where (t => Filter (t.flags)).Select (t => t.type).ToList ();

			bool Filter (HttpRequestTestFlags flags)
			{
				if (flags == HttpRequestTestFlags.GZip) {
					if (!setup.SupportsGZip)
						return false;
					flags = HttpRequestTestFlags.Working;
				}

				switch (category) {
				case HttpServerTestCategory.MartinTest:
					return false;
				case HttpServerTestCategory.Default:
					return flags == HttpRequestTestFlags.Working ||
						flags == HttpRequestTestFlags.WorkingRequireSSL;
				case HttpServerTestCategory.NoSsl:
					return flags == HttpRequestTestFlags.Working;
				case HttpServerTestCategory.Stress:
					return flags == HttpRequestTestFlags.Stress;
				case HttpServerTestCategory.NewWebStack:
					if (!setup.UsingDotNet &&
					    (flags == HttpRequestTestFlags.NewWebStackMono ||
					     flags == HttpRequestTestFlags.NewWebStackRequireSSL))
						return true;
					return flags == HttpRequestTestFlags.NewWebStack;
				case HttpServerTestCategory.NewWebStackNoSsl:
					if (flags == HttpRequestTestFlags.NewWebStackMono && !setup.UsingDotNet)
						return true;
					return flags == HttpRequestTestFlags.NewWebStack;
				case HttpServerTestCategory.Experimental:
					return flags == HttpRequestTestFlags.Unstable;
				default:
					throw ctx.AssertFail (category);
				}
			}
		}

		const int IdleTime = 750;

		protected override async Task RunSecondary (TestContext ctx, CancellationToken cancellationToken)
		{
			var me = $"{ME}.{nameof (RunSecondary)}()";

			Operation secondOperation = null;

			switch (EffectiveType) {
			case HttpRequestTestType.ReuseConnection:
			case HttpRequestTestType.ReuseConnection2:
			case HttpRequestTestType.ReuseAfterPartialRead:
			case HttpRequestTestType.CustomConnectionGroup:
			case HttpRequestTestType.ReuseCustomConnectionGroup:
				secondOperation = StartSecond (ctx, cancellationToken);
				break;
			case HttpRequestTestType.CloseIdleConnection:
				ctx.LogDebug (5, $"{me}: active connections: {PrimaryOperation.ServicePoint.CurrentConnections}");
				await Task.Delay ((int)(IdleTime * 2.5)).ConfigureAwait (false);
				ctx.LogDebug (5, $"{me}: active connections #1: {PrimaryOperation.ServicePoint.CurrentConnections}");
				ctx.Assert (PrimaryOperation.ServicePoint.CurrentConnections, Is.EqualTo (0), "current connections");
				break;
			case HttpRequestTestType.CloseCustomConnectionGroup:
				ctx.LogDebug (5, $"{me}: active connections: {PrimaryOperation.ServicePoint.CurrentConnections}");
				PrimaryOperation.ServicePoint.CloseConnectionGroup (((TraditionalRequest)PrimaryOperation.Request).RequestExt.ConnectionGroupName);
				ctx.LogDebug (5, $"{me}: active connections #1: {PrimaryOperation.ServicePoint.CurrentConnections}");
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
		}

		protected override (Handler handler, HttpOperationFlags flags) CreateHandler (TestContext ctx, bool primary)
		{
			var hello = new HelloWorldHandler (EffectiveType.ToString ());
			var helloKeepAlive = new HelloWorldHandler (EffectiveType.ToString ()) {
				Flags = RequestFlags.KeepAlive
			};
			var postHello = new PostHandler (EffectiveType.ToString (), HttpContent.HelloWorld);
			var chunkedPost = new PostHandler (EffectiveType.ToString (), HttpContent.HelloChunked, TransferMode.Chunked);

			switch (EffectiveType) {
			case HttpRequestTestType.SimpleNtlm:
				return (new AuthenticationHandler (AuthenticationType.NTLM, hello), HttpOperationFlags.None);
			case HttpRequestTestType.SimplePost:
				return (postHello, HttpOperationFlags.None);
			case HttpRequestTestType.SimpleRedirect:
				return (new RedirectHandler (hello, HttpStatusCode.Redirect), HttpOperationFlags.None);
			case HttpRequestTestType.PostNtlm:
				return (new AuthenticationHandler (AuthenticationType.NTLM, postHello), HttpOperationFlags.None);
			case HttpRequestTestType.PostRedirect:
				return (new RedirectHandler (postHello, HttpStatusCode.TemporaryRedirect), HttpOperationFlags.None);
			case HttpRequestTestType.NtlmChunked:
				return (new AuthenticationHandler (AuthenticationType.NTLM, chunkedPost), HttpOperationFlags.None);
			case HttpRequestTestType.Get404:
				return (new GetHandler (EffectiveType.ToString (), null, HttpStatusCode.NotFound), HttpOperationFlags.None);
			case HttpRequestTestType.RedirectNoReuse:
				return (new RedirectHandler (hello, HttpStatusCode.Redirect), HttpOperationFlags.None);
			case HttpRequestTestType.GetChunked:
				return (new GetHandler (EffectiveType.ToString (), HttpContent.HelloChunked), HttpOperationFlags.None);
			case HttpRequestTestType.Simple:
				return (hello, HttpOperationFlags.None);
			default:
				var handler = new HttpRequestHandler (this, primary);
				return (handler, handler.OperationFlags);
			}
		}

		internal AuthenticationManager GetAuthenticationManager ()
		{
			var manager = new AuthenticationManager (AuthenticationType.NTLM, AuthenticationHandler.GetCredentials ());
			var old = Interlocked.CompareExchange (ref authManager, manager, null);
			return old ?? manager;
		}

		protected override InstrumentationOperation CreateOperation (
			TestContext ctx, Handler handler, InstrumentationOperationType type, HttpOperationFlags flags)
		{
			HttpStatusCode expectedStatus;
			WebExceptionStatus expectedError;

			switch (EffectiveType) {
			case HttpRequestTestType.CloseRequestStream:
				expectedStatus = HttpStatusCode.InternalServerError;
				expectedError = WebExceptionStatus.RequestCanceled;
				break;
			case HttpRequestTestType.ReadTimeout:
				expectedStatus = HttpStatusCode.InternalServerError;
				expectedError = WebExceptionStatus.Timeout;
				break;
			case HttpRequestTestType.Get404:
				expectedStatus = HttpStatusCode.NotFound;
				expectedError = WebExceptionStatus.ProtocolError;
				break;
			case HttpRequestTestType.ServerAbortsPost:
				expectedStatus = HttpStatusCode.BadRequest;
				expectedError = WebExceptionStatus.ProtocolError;
				break;
			case HttpRequestTestType.EntityTooBig:
			case HttpRequestTestType.ClientAbortsPost:
				expectedStatus = HttpStatusCode.InternalServerError;
				expectedError = WebExceptionStatus.AnyErrorStatus;
				break;
			default:
				expectedStatus = HttpStatusCode.OK;
				expectedError = WebExceptionStatus.Success;
				break;
			}

			return new Operation (this, handler, type, flags, expectedStatus, expectedError);
		}

		internal async Task HandleRequest (
			TestContext ctx, HttpRequestHandler handler,
			HttpConnection connection, HttpRequest request,
			AuthenticationState state, CancellationToken cancellationToken)
		{
			switch (EffectiveType) {
			case HttpRequestTestType.ReuseConnection:
			case HttpRequestTestType.ReuseCustomConnectionGroup:
			case HttpRequestTestType.RedirectOnSameConnection:
				MustReuseConnection ();
				break;

			case HttpRequestTestType.ParallelNtlm:
				await ParallelNtlm ().ConfigureAwait (false);
				break;

			case HttpRequestTestType.ReuseAfterPartialRead:
				// We can't reuse the connection because we did not read the entire response.
				MustNotReuseConnection ();
				break;

			case HttpRequestTestType.CustomConnectionGroup:
				// We can't reuse the connection because we're in a different connection group.
				MustNotReuseConnection ();
				break;

			case HttpRequestTestType.NtlmInstrumentation:
				break;
			}

			async Task ParallelNtlm ()
			{
				var firstHandler = (HttpRequestHandler)PrimaryOperation.Handler;
				ctx.LogDebug (2, $"{handler.ME}: {handler == firstHandler} {state}");
				if (handler != firstHandler || state != AuthenticationState.Challenge)
					return;

				var newHandler = (HttpRequestHandler)firstHandler.Clone ();
				var flags = PrimaryOperation.Flags;

				var operation = StartOperation (ctx, cancellationToken, newHandler, InstrumentationOperationType.Queued, flags);
				await operation.WaitForRequest ();
			}

			void MustNotReuseConnection ()
			{
				var firstHandler = (HttpRequestHandler)PrimaryOperation.Handler;
				ctx.LogDebug (2, $"{handler.ME}: {handler == firstHandler} {handler.RemoteEndPoint}");
				if (handler == firstHandler)
					return;
				ctx.Assert (connection.RemoteEndPoint, Is.Not.EqualTo (firstHandler.RemoteEndPoint), "RemoteEndPoint");
			}

			void MustReuseConnection ()
			{
				var firstHandler = (HttpRequestHandler)PrimaryOperation.Handler;
				ctx.LogDebug (2, $"{handler.ME}: {handler == firstHandler} {handler.RemoteEndPoint}");
				if (handler == firstHandler)
					return;
				ctx.Assert (connection.RemoteEndPoint, Is.EqualTo (firstHandler.RemoteEndPoint), "RemoteEndPoint");
			}
		}

		Operation StartSecond (TestContext ctx, CancellationToken cancellationToken,
				       HttpStatusCode expectedStatus = HttpStatusCode.OK,
				       WebExceptionStatus expectedError = WebExceptionStatus.Success)
		{
			var (handler, flags) = CreateHandler (ctx, false);
			var operation = new Operation (this, handler, InstrumentationOperationType.Parallel, flags, expectedStatus, expectedError);
			operation.Start (ctx, cancellationToken);
			return operation;
		}

		protected override Task PrimaryReadHandler (TestContext ctx, CancellationToken cancellationToken)
		{
			throw new NotImplementedException();
		}

		protected override Task SecondaryReadHandler (TestContext ctx, CancellationToken cancellationToken)
		{
			throw new NotImplementedException();
		}

		class Operation : InstrumentationOperation
		{
			new public HttpRequestTestRunner Parent => (HttpRequestTestRunner)base.Parent;

			public HttpRequestTestType EffectiveType => Parent.EffectiveType;

			public HttpRequestHandler InstrumentationHandler => (HttpRequestHandler)base.Handler;

			public Operation (HttpRequestTestRunner parent, Handler handler,
					  InstrumentationOperationType type, HttpOperationFlags flags,
					  HttpStatusCode expectedStatus, WebExceptionStatus expectedError)
				: base (parent, $"{parent.EffectiveType}:{type}",
					handler, type, flags, expectedStatus, expectedError)
			{
			}

			protected override Request CreateRequest (TestContext ctx, Uri uri)
			{
				var primary = Type == InstrumentationOperationType.Primary;
				if (Handler is HttpRequestHandler instrumentationHandler)
					return instrumentationHandler.CreateRequest (ctx, primary, uri);

				return new TraditionalRequest (uri);
			}

			protected override void ConfigureRequest (TestContext ctx, Uri uri, Request request)
			{
				var traditionalRequest = (TraditionalRequest)request;

				if (Type == InstrumentationOperationType.Primary)
					ConfigurePrimaryRequest (ctx, traditionalRequest);
				else
					ConfigureParallelRequest (ctx, traditionalRequest);

				Handler.ConfigureRequest (request, uri);

				request.SetProxy (Parent.Server.GetProxy ());
			}

			void ConfigureParallelRequest (TestContext ctx, TraditionalRequest request)
			{
				switch (EffectiveType) {
				case HttpRequestTestType.ReuseConnection:
				case HttpRequestTestType.ReuseConnection2:
				case HttpRequestTestType.ReuseAfterPartialRead:
				case HttpRequestTestType.ParallelNtlm:
				case HttpRequestTestType.CustomConnectionGroup:
					break;
				case HttpRequestTestType.ReuseCustomConnectionGroup:
					request.RequestExt.ConnectionGroupName = "custom";
					break;
				default:
					throw ctx.AssertFail (Parent.EffectiveType);
				}
			}

			void ConfigurePrimaryRequest (TestContext ctx, TraditionalRequest request)
			{
				request.RequestExt.ReadWriteTimeout = int.MaxValue;
				request.RequestExt.Timeout = int.MaxValue;

				switch (EffectiveType) {
				case HttpRequestTestType.SimplePost:
					request.SetContentLength (((PostHandler)Handler).Content.Length);
					break;
				case HttpRequestTestType.CustomConnectionGroup:
				case HttpRequestTestType.ReuseCustomConnectionGroup:
					request.RequestExt.ConnectionGroupName = "custom";
					break;
				case HttpRequestTestType.CloseIdleConnection:
					ServicePoint.MaxIdleTime = IdleTime;
					break;
				}
			}

			protected override Task<Response> RunInner (TestContext ctx, Request request, CancellationToken cancellationToken)
			{
				ctx.LogDebug (2, $"{ME} RUN INNER");
				switch (EffectiveType) {
				case HttpRequestTestType.ServerAbortsPost:
					return ((TraditionalRequest)request).Send (ctx, cancellationToken);
				default:
					return ((TraditionalRequest)request).SendAsync (ctx, cancellationToken);
				}
			}

			protected override void ConfigureNetworkStream (TestContext ctx, StreamInstrumentation instrumentation)
			{
				switch (EffectiveType) {
				case HttpRequestTestType.CloseCustomConnectionGroup:
					instrumentation.IgnoreErrors = true;
					break;
				}
			}

			protected override Task ReadHandler (TestContext ctx, byte[] buffer, int offset, int size, int ret, CancellationToken cancellationToken)
			{
				throw new NotImplementedException ();
			}
		}

		AuthenticationManager authManager;

		static bool ExpectWebException (TestContext ctx, Task task, WebExceptionStatus status, string message)
		{
			if (!ctx.Expect (task.Status, Is.EqualTo (TaskStatus.Faulted), message))
				return false;
			var error = TestContext.CleanupException (task.Exception);
			if (!ctx.Expect (error, Is.InstanceOf<WebException> (), message))
				return false;
			return ctx.Expect (((WebException)error).Status, Is.EqualTo (status), message);
		}
	}
}
