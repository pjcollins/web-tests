//
// HttpInstrumentationHandler.cs
//
// Author:
//       Martin Baulig <mabaul@microsoft.com>
//
// Copyright (c) 2018 
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
	using Resources;
	using Xamarin.WebTests.Server;

	public class HttpInstrumentationHandler : Handler
	{
		public HttpInstrumentationTestRunner TestRunner {
			get;
		}

		public bool CloseConnection {
			get;
		}

		public HttpContent Content {
			get;
		}

		public HttpContent ExpectedContent {
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

		public HttpOperationFlags OperationFlags {
			get;
		}

		public bool IsSecondRequest {
			get;
			private set;
		}

		public string ME {
			get;
		}

		TaskCompletionSource<bool> readyTcs;

		public HttpInstrumentationHandler (HttpInstrumentationTestRunner parent, bool primary)
			: base (parent.EffectiveType.ToString ())
		{
			TestRunner = parent;
			ME = $"{GetType ().Name}({parent.EffectiveType})";
			readyTcs = new TaskCompletionSource<bool> ();
			Flags = RequestFlags.KeepAlive;

			switch (parent.EffectiveType) {
			case HttpInstrumentationTestType.RedirectOnSameConnection:
			case HttpInstrumentationTestType.RedirectNoLength:
				Target = new HelloWorldHandler (ME);
				break;
			}

			switch (parent.EffectiveType) {
			case HttpInstrumentationTestType.ReuseConnection:
				CloseConnection = !primary;
				break;
			case HttpInstrumentationTestType.ReuseAfterPartialRead:
				Content = ConnectionHandler.GetLargeStringContent (2500);
				OperationFlags = HttpOperationFlags.ClientUsesNewConnection;
				CloseConnection = !primary;
				break;
			case HttpInstrumentationTestType.ReuseConnection2:
				Content = HttpContent.HelloWorld;
				CloseConnection = !primary;
				break;
			case HttpInstrumentationTestType.CloseIdleConnection:
			case HttpInstrumentationTestType.CloseCustomConnectionGroup:
				CloseConnection = false;
				break;
			case HttpInstrumentationTestType.NtlmClosesConnection:
				AuthManager = parent.GetAuthenticationManager ();
				CloseConnection = true;
				break;
			case HttpInstrumentationTestType.NtlmReusesConnection:
				AuthManager = parent.GetAuthenticationManager ();
				CloseConnection = false;
				break;
			case HttpInstrumentationTestType.ParallelNtlm:
			case HttpInstrumentationTestType.NtlmInstrumentation:
			case HttpInstrumentationTestType.NtlmWhileQueued:
			case HttpInstrumentationTestType.NtlmWhileQueued2:
				AuthManager = parent.GetAuthenticationManager ();
				CloseConnection = false;
				break;
			case HttpInstrumentationTestType.LargeHeader:
			case HttpInstrumentationTestType.LargeHeader2:
			case HttpInstrumentationTestType.SendResponseAsBlob:
				Content = ConnectionHandler.TheQuickBrownFoxContent;
				CloseConnection = true;
				break;
			case HttpInstrumentationTestType.CustomConnectionGroup:
				OperationFlags = HttpOperationFlags.DontReuseConnection | HttpOperationFlags.ForceNewConnection;
				CloseConnection = !primary;
				break;
			case HttpInstrumentationTestType.ReuseCustomConnectionGroup:
			case HttpInstrumentationTestType.ReadTimeout:
			case HttpInstrumentationTestType.AbortResponse:
				CloseConnection = !primary;
				break;
			case HttpInstrumentationTestType.CloseRequestStream:
				OperationFlags = HttpOperationFlags.AbortAfterClientExits;
				CloseConnection = !primary;
				break;
			case HttpInstrumentationTestType.RedirectOnSameConnection:
				Target = new HelloWorldHandler (ME);
				CloseConnection = false;
				break;
			case HttpInstrumentationTestType.RedirectNoLength:
				Target = new HelloWorldHandler (ME);
				OperationFlags |= HttpOperationFlags.RedirectOnNewConnection;
				CloseConnection = false;
				break;
			case HttpInstrumentationTestType.PutChunked:
			case HttpInstrumentationTestType.PutChunkDontCloseRequest:
				CloseConnection = true;
				break;
			case HttpInstrumentationTestType.ServerAbortsRedirect:
				OperationFlags = HttpOperationFlags.ServerAbortsRedirection;
				CloseConnection = false;
				break;
			case HttpInstrumentationTestType.ServerAbortsPost:
				OperationFlags = HttpOperationFlags.ServerAbortsRedirection;
				CloseConnection = true;
				break;
			case HttpInstrumentationTestType.PostChunked:
				OperationFlags = HttpOperationFlags.DontReadRequestBody;
				CloseConnection = false;
				break;
			case HttpInstrumentationTestType.EntityTooBig:
			case HttpInstrumentationTestType.ClientAbortsPost:
				OperationFlags = HttpOperationFlags.AbortAfterClientExits | HttpOperationFlags.DontReadRequestBody;
				CloseConnection = false;
				break;
			case HttpInstrumentationTestType.PostContentLength:
				OperationFlags = HttpOperationFlags.DontReadRequestBody;
				break;
			case HttpInstrumentationTestType.SimpleGZip:
				Content = HttpContent.TheQuickBrownFox;
				CloseConnection = true;
				break;
			case HttpInstrumentationTestType.TestResponseStream:
				Content = new StringContent ("AAAA");
				CloseConnection = true;
				break;
			case HttpInstrumentationTestType.LargeChunkRead:
				Content = HttpContent.TheQuickBrownFoxChunked;
				ExpectedContent = Content.RemoveTransferEncoding ();
				CloseConnection = false;
				break;
			case HttpInstrumentationTestType.LargeGZipRead:
				Content = ConnectionHandler.GetLargeChunkedContent (16384);
				ExpectedContent = Content.RemoveTransferEncoding ();
				CloseConnection = false;
				break;
			case HttpInstrumentationTestType.GZipWithLength:
				Content = ConnectionHandler.GetLargeStringContent (16384);
				ExpectedContent = Content;
				CloseConnection = false;
				break;
			case HttpInstrumentationTestType.ResponseStreamCheckLength2:
				Content = HttpContent.HelloChunked;
				ExpectedContent = Content.RemoveTransferEncoding ();
				CloseConnection = false;
				break;
			case HttpInstrumentationTestType.ResponseStreamCheckLength:
				Content = HttpContent.HelloWorld;
				ExpectedContent = Content;
				CloseConnection = false;
				break;
			case HttpInstrumentationTestType.GetNoLength:
				ExpectedContent = ConnectionHandler.TheQuickBrownFoxContent;
				CloseConnection = false;
				break;
			case HttpInstrumentationTestType.ImplicitHost:
			case HttpInstrumentationTestType.CustomHost:
			case HttpInstrumentationTestType.CustomHostWithPort:
			case HttpInstrumentationTestType.CustomHostDefaultPort:
				CloseConnection = false;
				break;
			default:
				throw new NotSupportedException (parent.EffectiveType.ToString ());
			}

			if (CloseConnection)
				Flags |= RequestFlags.CloseConnection;

			if (AuthManager != null)
				Target = new HelloWorldHandler (ME);

			if (ExpectedContent == null)
				ExpectedContent = Content ?? new StringContent (ME);
		}

		HttpInstrumentationHandler (HttpInstrumentationHandler other)
			: base (other.Value)
		{
			TestRunner = other.TestRunner;
			Content = other.Content;
			CloseConnection = CloseConnection;
			ME = other.ME;
			Flags = other.Flags;
			Target = other.Target;
			AuthManager = other.AuthManager;
			readyTcs = new TaskCompletionSource<bool> ();
		}

		HttpInstrumentationRequest currentRequest;

		public override object Clone ()
		{
			return new HttpInstrumentationHandler (this);
		}

		public Request CreateRequest (
			TestContext ctx, bool primary, Uri uri)
		{
			switch (TestRunner.EffectiveType) {
			case HttpInstrumentationTestType.ReuseAfterPartialRead:
			case HttpInstrumentationTestType.CloseRequestStream:
			case HttpInstrumentationTestType.ReadTimeout:
			case HttpInstrumentationTestType.AbortResponse:
				return new HttpInstrumentationRequest (this, uri);
			case HttpInstrumentationTestType.NtlmWhileQueued:
			case HttpInstrumentationTestType.NtlmWhileQueued2:
				if (primary)
					return new HttpInstrumentationRequest (this, uri);
				return new TraditionalRequest (uri);

			case HttpInstrumentationTestType.PutChunked:
			case HttpInstrumentationTestType.PutChunkDontCloseRequest:
				return new HttpInstrumentationRequest (this, uri) {
					Content = ConnectionHandler.GetLargeStringContent (50)
				};
			case HttpInstrumentationTestType.PostChunked:
			case HttpInstrumentationTestType.EntityTooBig:
			case HttpInstrumentationTestType.PostContentLength:
			case HttpInstrumentationTestType.ClientAbortsPost:
			case HttpInstrumentationTestType.SimpleGZip:
			case HttpInstrumentationTestType.TestResponseStream:
			case HttpInstrumentationTestType.LargeChunkRead:
			case HttpInstrumentationTestType.LargeGZipRead:
			case HttpInstrumentationTestType.GZipWithLength:
			case HttpInstrumentationTestType.ResponseStreamCheckLength2:
			case HttpInstrumentationTestType.ResponseStreamCheckLength:
			case HttpInstrumentationTestType.GetNoLength:
				return new HttpInstrumentationRequest (this, uri);
			default:
				return new TraditionalRequest (uri);
			}
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

			case HttpInstrumentationTestType.ReadTimeout:
				currentRequest.RequestExt.ReadWriteTimeout = 100;
				break;

			case HttpInstrumentationTestType.PutChunked:
			case HttpInstrumentationTestType.PutChunkDontCloseRequest:
				request.Method = "PUT";
				request.SetContentType ("application/octet-stream");
				request.SetContentLength (request.Content.Length);
				request.SendChunked ();
				break;

			case HttpInstrumentationTestType.ServerAbortsPost:
				request.Method = "POST";
				request.SetContentType ("application/x-www-form-urlencoded");
				request.Content = new FormContent (("foo", "bar"), ("hello", "world"), ("escape", "this needs % escaping"));
				break;

			case HttpInstrumentationTestType.EntityTooBig:
			case HttpInstrumentationTestType.PostContentLength:
			case HttpInstrumentationTestType.ClientAbortsPost:
				request.Method = "POST";
				request.SetContentType ("text/plain");
				request.SetContentLength (request.Content.Length);
				break;

			case HttpInstrumentationTestType.SimpleGZip:
				break;

			case HttpInstrumentationTestType.ImplicitHost:
				break;

			case HttpInstrumentationTestType.CustomHost:
				((TraditionalRequest)request).RequestExt.Host = "custom";
				break;

			case HttpInstrumentationTestType.CustomHostWithPort:
				((TraditionalRequest)request).RequestExt.Host = "custom:8888";
				break;

			case HttpInstrumentationTestType.CustomHostDefaultPort:
				var defaultPort = TestRunner.Server.UseSSL ? 443 : 80;
				((TraditionalRequest)request).RequestExt.Host = $"custom:{defaultPort}";
				break;
			}

			base.ConfigureRequest (request, uri);
		}

		async Task<HttpResponse> HandleNtlmRequest (
			TestContext ctx, HttpOperation operation, HttpConnection connection, HttpRequest request,
			RequestFlags effectiveFlags, CancellationToken cancellationToken)
		{
			var me = $"{ME}.{nameof (HandleNtlmRequest)}";
			ctx.LogDebug (3, $"{me}: {connection.RemoteEndPoint}");

			AuthenticationState state;
			var response = AuthManager.HandleAuthentication (ctx, connection, request, out state);
			ctx.LogDebug (3, $"{me}: {connection.RemoteEndPoint} - {state} {response}");

			if (state == AuthenticationState.Unauthenticated) {
				ctx.Assert (RemoteEndPoint, Is.Null, "first request");
				RemoteEndPoint = connection.RemoteEndPoint;
			} else if (TestRunner.EffectiveType == HttpInstrumentationTestType.NtlmInstrumentation) {
				if (state == AuthenticationState.Challenge) {
					ctx.LogDebug (3, $"{me}: {connection.RemoteEndPoint} {RemoteEndPoint}");
					RemoteEndPoint = connection.RemoteEndPoint;
				} else
					ctx.Assert (connection.RemoteEndPoint, Is.EqualTo (RemoteEndPoint), "must reuse connection");
			}

			await TestRunner.HandleRequest (
				ctx, this, connection, request, state, cancellationToken).ConfigureAwait (false);

			var keepAlive = !CloseConnection && (effectiveFlags & (RequestFlags.KeepAlive | RequestFlags.CloseConnection)) == RequestFlags.KeepAlive;
			if (response != null) {
				response.Redirect = operation.RegisterRedirect (ctx, this, request.Path);
				return response;
			}

			cancellationToken.ThrowIfCancellationRequested ();

			HttpInstrumentationContent content;
			switch (TestRunner.EffectiveType) {
			case HttpInstrumentationTestType.NtlmWhileQueued:
				content = new HttpInstrumentationContent (TestRunner, currentRequest);
				return new HttpResponse (HttpStatusCode.OK, content);
			case HttpInstrumentationTestType.NtlmWhileQueued2:
				content = new HttpInstrumentationContent (TestRunner, currentRequest);
				return new HttpResponse (HttpStatusCode.OK, content) { CloseConnection = true };
			}

			var ret = await Target.HandleRequest (ctx, operation, connection, request, effectiveFlags, cancellationToken);
			ctx.LogDebug (3, $"{me} target done: {Target} {ret}");
			ret.KeepAlive = false;
			return ret;
		}

		async Task<HttpResponse> HandlePostChunked (
			TestContext ctx, HttpOperation operation, HttpConnection connection, HttpRequest request,
			RequestFlags effectiveFlags, CancellationToken cancellationToken)
		{
			var me = $"{ME}.{nameof (HandlePostChunked)}";
			ctx.LogDebug (3, $"{me}: {connection.RemoteEndPoint}");

			var firstChunk = await ChunkedContent.ReadChunk (ctx, request.Reader, cancellationToken).ConfigureAwait (false);
			ctx.LogDebug (3, $"{me} got first chunk: {firstChunk.Length}");

			ctx.Assert (firstChunk, Is.EqualTo (ConnectionHandler.TheQuickBrownFoxBuffer), "first chunk");

			readyTcs.TrySetResult (true);

			ctx.LogDebug (3, $"{me} reading remaining body");

			await ChunkedContent.Read (ctx, request.Reader, cancellationToken).ConfigureAwait (false);

			await TestRunner.HandleRequest (
				ctx, this, connection, request, AuthenticationState.None, cancellationToken).ConfigureAwait (false);

			return HttpResponse.CreateSuccess (ME);
		}

		internal Task WaitUntilReady ()
		{
			return readyTcs.Task;
		}

		protected internal override async Task<HttpResponse> HandleRequest (
			TestContext ctx, HttpOperation operation, HttpConnection connection, HttpRequest request,
			RequestFlags effectiveFlags, CancellationToken cancellationToken)
		{
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
			case HttpInstrumentationTestType.AbortResponse:
			case HttpInstrumentationTestType.RedirectOnSameConnection:
			case HttpInstrumentationTestType.SimpleGZip:
			case HttpInstrumentationTestType.TestResponseStream:
			case HttpInstrumentationTestType.LargeChunkRead:
			case HttpInstrumentationTestType.LargeGZipRead:
			case HttpInstrumentationTestType.GZipWithLength:
			case HttpInstrumentationTestType.ResponseStreamCheckLength2:
			case HttpInstrumentationTestType.ResponseStreamCheckLength:
			case HttpInstrumentationTestType.GetNoLength:
				ctx.Assert (request.Method, Is.EqualTo ("GET"), "method");
				break;

			case HttpInstrumentationTestType.ReuseConnection2:
			case HttpInstrumentationTestType.ServerAbortsPost:
				ctx.Assert (request.Method, Is.EqualTo ("POST"), "method");
				break;

			case HttpInstrumentationTestType.NtlmInstrumentation:
			case HttpInstrumentationTestType.NtlmClosesConnection:
			case HttpInstrumentationTestType.NtlmReusesConnection:
			case HttpInstrumentationTestType.ParallelNtlm:
			case HttpInstrumentationTestType.NtlmWhileQueued:
			case HttpInstrumentationTestType.NtlmWhileQueued2:
				return await HandleNtlmRequest (
					ctx, operation, connection, request, effectiveFlags, cancellationToken).ConfigureAwait (false);

			case HttpInstrumentationTestType.RedirectNoLength:
			case HttpInstrumentationTestType.PutChunked:
			case HttpInstrumentationTestType.PutChunkDontCloseRequest:
			case HttpInstrumentationTestType.ServerAbortsRedirect:
				break;

			case HttpInstrumentationTestType.EntityTooBig:
				await EntityTooBig ().ConfigureAwait (false);
				return null;

			case HttpInstrumentationTestType.PostChunked:
				return await HandlePostChunked (
					ctx, operation, connection, request, effectiveFlags, cancellationToken).ConfigureAwait (false);

			case HttpInstrumentationTestType.PostContentLength:
				await PostContentLength ().ConfigureAwait (false);
				break;

			case HttpInstrumentationTestType.ClientAbortsPost:
				await ClientAbortsPost ().ConfigureAwait (false);
				return null;

			case HttpInstrumentationTestType.ImplicitHost:
				var hostAndPort = TestRunner.Uri.GetComponents (UriComponents.HostAndPort, UriFormat.Unescaped);
				ctx.Assert (request.Headers["Host"], Is.EqualTo (hostAndPort), "host");
				break;

			case HttpInstrumentationTestType.CustomHost:
				ctx.Assert (request.Headers["Host"], Is.EqualTo ("custom"), "host");
				break;

			case HttpInstrumentationTestType.CustomHostWithPort:
				ctx.Assert (request.Headers["Host"], Is.EqualTo ("custom:8888"), "host");
				break;

			case HttpInstrumentationTestType.CustomHostDefaultPort:
				var defaultPort = TestRunner.Server.UseSSL ? 443 : 80;
				ctx.Assert (request.Headers["Host"], Is.EqualTo ($"custom:{defaultPort}"), "host");
				break;

			default:
				throw ctx.AssertFail (TestRunner.EffectiveType);
			}

			RemoteEndPoint = connection.RemoteEndPoint;

			await TestRunner.HandleRequest (
				ctx, this, connection, request, AuthenticationState.None, cancellationToken).ConfigureAwait (false);

			HttpResponse response;
			HttpInstrumentationContent content;
			ListenerOperation redirect;

			switch (TestRunner.EffectiveType) {
			case HttpInstrumentationTestType.LargeHeader:
				response = new HttpResponse (HttpStatusCode.OK, Content);
				response.AddHeader ("LargeTest", ConnectionHandler.GetLargeText (100));
				return response;

			case HttpInstrumentationTestType.LargeHeader2:
				response = new HttpResponse (HttpStatusCode.OK, Content);
				response.AddHeader ("LargeTest", ConnectionHandler.GetLargeText (100));
				response.WriteAsBlob = true;
				return response;

			case HttpInstrumentationTestType.SendResponseAsBlob:
				return new HttpResponse (HttpStatusCode.OK, Content) {
					WriteAsBlob = true
				};

			case HttpInstrumentationTestType.ReuseAfterPartialRead:
				return new HttpResponse (HttpStatusCode.OK, Content) {
					WriteAsBlob = true
				};

			case HttpInstrumentationTestType.ReadTimeout:
			case HttpInstrumentationTestType.AbortResponse:
				content = new HttpInstrumentationContent (TestRunner, currentRequest);
				return new HttpResponse (HttpStatusCode.OK, content);

			case HttpInstrumentationTestType.ReuseConnection2:
				return new HttpResponse (HttpStatusCode.OK, Content);

			case HttpInstrumentationTestType.RedirectOnSameConnection:
				redirect = operation.RegisterRedirect (ctx, Target);
				response = HttpResponse.CreateRedirect (HttpStatusCode.Redirect, redirect);
				response.SetBody (new StringContent ($"{ME} Redirecting"));
				response.WriteAsBlob = true;
				return response;

			case HttpInstrumentationTestType.RedirectNoLength:
				redirect = operation.RegisterRedirect (ctx, Target);
				response = HttpResponse.CreateRedirect (HttpStatusCode.Redirect, redirect);
				response.NoContentLength = true;
				return response;

			case HttpInstrumentationTestType.ServerAbortsRedirect:
				if (IsSecondRequest)
					throw ctx.AssertFail ("Should never happen.");
				var cloned = new HttpInstrumentationHandler (this);
				cloned.IsSecondRequest = true;
				redirect = operation.RegisterRedirect (ctx, cloned);
				response = HttpResponse.CreateRedirect (HttpStatusCode.Redirect, redirect);
				return response;

			case HttpInstrumentationTestType.ServerAbortsPost:
				return new HttpResponse (HttpStatusCode.BadRequest, Content);

			case HttpInstrumentationTestType.SimpleGZip:
				var gzipContent = new GZipContent (ConnectionHandler.TheQuickBrownFoxBuffer);
				return new HttpResponse (HttpStatusCode.OK, gzipContent);

			case HttpInstrumentationTestType.TestResponseStream:
				response = new HttpResponse (HttpStatusCode.OK, Content);
				response.WriteAsBlob = true;
				return response;

			case HttpInstrumentationTestType.LargeChunkRead:
				response = new HttpResponse (HttpStatusCode.OK, Content);
				response.WriteBodyAsBlob = true;
				return response;

			case HttpInstrumentationTestType.LargeGZipRead:
				gzipContent = new GZipContent ((ChunkedContent)Content);
				response = new HttpResponse (HttpStatusCode.OK, gzipContent);
				return response;

			case HttpInstrumentationTestType.GZipWithLength:
				gzipContent = new GZipContent ((StringContent)Content);
				response = new HttpResponse (HttpStatusCode.OK, gzipContent);
				return response;

			case HttpInstrumentationTestType.ResponseStreamCheckLength2:
			case HttpInstrumentationTestType.ResponseStreamCheckLength:
				response = new HttpResponse (HttpStatusCode.OK, Content);
				return response;

			case HttpInstrumentationTestType.GetNoLength:
				content = new HttpInstrumentationContent (TestRunner, currentRequest);
				return new HttpResponse (HttpStatusCode.OK, content);

			default:
				return HttpResponse.CreateSuccess (ME);
			}

			async Task EntityTooBig ()
			{
				await request.ReadHeaders (ctx, cancellationToken).ConfigureAwait (false);
				await ctx.AssertException<IOException> (() => request.Read (ctx, cancellationToken), "client doesn't send any body");
			}

			async Task ClientAbortsPost ()
			{
				await request.ReadHeaders (ctx, cancellationToken).ConfigureAwait (false);
				await ctx.AssertException<IOException> (() => request.Read (ctx, cancellationToken), "client doesn't send any body");
			}

			async Task PostContentLength ()
			{
				await request.ReadHeaders (ctx, cancellationToken).ConfigureAwait (false);
				ctx.Assert (request.ContentLength, Is.EqualTo (currentRequest.Content.Length), "request.ContentLength");
				readyTcs.TrySetResult (true);
				await request.Read (ctx, cancellationToken);
			}
		}

		public override bool CheckResponse (TestContext ctx, Response response)
		{
			if (Target != null)
				return Target.CheckResponse (ctx, response);

			switch (TestRunner.EffectiveType) {
			case HttpInstrumentationTestType.ReadTimeout:
			case HttpInstrumentationTestType.AbortResponse:
				return ctx.Expect (response.Status, Is.EqualTo (HttpStatusCode.OK), "response.StatusCode");

			case HttpInstrumentationTestType.ReuseAfterPartialRead:
				if (!ctx.Expect (response.Content, Is.Not.Null, "response.Content != null"))
					return false;

				return ctx.Expect (response.Content.Length, Is.GreaterThan (0), "response.Content.Length");
			}

			if (!ctx.Expect (response.Content, Is.Not.Null, "response.Content != null"))
				return false;

			return HttpContent.Compare (ctx, response.Content, ExpectedContent, false, "response.Content");
		}

		public override Task CheckResponse (TestContext ctx, Response response,
						    CancellationToken cancellationToken,
						    HttpStatusCode expectedStatus = HttpStatusCode.OK,
						    WebExceptionStatus expectedError = WebExceptionStatus.Success)
		{
			switch (TestRunner.EffectiveType) {
			case HttpInstrumentationTestType.EntityTooBig:
			default:
				return base.CheckResponse (ctx, response, cancellationToken, expectedStatus, expectedError);
			}
		}

		class HttpInstrumentationRequest : TraditionalRequest
		{
			public HttpInstrumentationHandler Handler {
				get;
			}

			public HttpInstrumentationTestRunner TestRunner {
				get;
			}

			public string ME {
				get;
			}

			TaskCompletionSource<bool> finishedTcs;

			public Task WaitForCompletion ()
			{
				return finishedTcs.Task;
			}

			public HttpInstrumentationRequest (HttpInstrumentationHandler handler, Uri uri)
				: base (uri)
			{
				Handler = handler;
				TestRunner = handler.TestRunner;
				finishedTcs = new TaskCompletionSource<bool> ();
				ME = $"{GetType ().Name}({TestRunner.EffectiveType})";

				switch (TestRunner.EffectiveType) {
				case HttpInstrumentationTestType.PostChunked:
					Content = new HttpInstrumentationContent (TestRunner, this);
					Method = "POST";
					SendChunked ();
					break;
				case HttpInstrumentationTestType.EntityTooBig:
				case HttpInstrumentationTestType.PostContentLength:
				case HttpInstrumentationTestType.ClientAbortsPost:
					Content = new HttpInstrumentationContent (TestRunner, this);
					Method = "POST";
					break;
				case HttpInstrumentationTestType.SimpleGZip:
				case HttpInstrumentationTestType.LargeGZipRead:
				case HttpInstrumentationTestType.GZipWithLength:
					RequestExt.AutomaticDecompression = true;
					break;
				}
			}

			protected override Task WriteBody (TestContext ctx, CancellationToken cancellationToken)
			{
				switch (TestRunner.EffectiveType) {
				case HttpInstrumentationTestType.PutChunked:
				case HttpInstrumentationTestType.PutChunkDontCloseRequest:
					return PutChunked ();

				case HttpInstrumentationTestType.EntityTooBig:
					return EntityTooBig ();

				case HttpInstrumentationTestType.PostContentLength:
					return PostContentLength ();

				case HttpInstrumentationTestType.ClientAbortsPost:
					return ClientAbortsPost ();

				default:
					return base.WriteBody (ctx, cancellationToken);
				}

				async Task EntityTooBig ()
				{
					var stream = await RequestExt.GetRequestStreamAsync ().ConfigureAwait (false);
					await Content.WriteToAsync (ctx, stream, cancellationToken).ConfigureAwait (false);
					// This throws on .NET
					try { stream.Dispose (); } catch { }
				}

				async Task PostContentLength ()
				{
					using (var stream = await RequestExt.GetRequestStreamAsync ().ConfigureAwait (false)) {
						await AbstractConnection.WaitWithTimeout (ctx, 1500, Handler.WaitUntilReady ());
						await Content.WriteToAsync (ctx, stream, cancellationToken);
						stream.Flush ();
					}
				}

				async Task PutChunked ()
				{
					var stream = await RequestExt.GetRequestStreamAsync ().ConfigureAwait (false);
					try {
						await Content.WriteToAsync (ctx, stream, cancellationToken).ConfigureAwait (false);
						await stream.FlushAsync ();
					} finally {
						if (TestRunner.EffectiveType == HttpInstrumentationTestType.PutChunked)
							stream.Dispose ();
					}
				}

				async Task ClientAbortsPost ()
				{
					var stream = await RequestExt.GetRequestStreamAsync ().ConfigureAwait (false);
					try {
						stream.Dispose ();
					} catch (Exception ex) {
						ctx.LogMessage ($"{ME} GOT EX: {ex.Message}");
					}
				}
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

				ctx.LogDebug (4, $"{ME} GET RESPONSE FROM HTTP");

				switch (TestRunner.EffectiveType) {
				case HttpInstrumentationTestType.ReadTimeout:
					return await ReadWithTimeout (5000, WebExceptionStatus.Timeout).ConfigureAwait (false);

				case HttpInstrumentationTestType.AbortResponse:
				case HttpInstrumentationTestType.NtlmWhileQueued:
					return await ReadWithTimeout (0, WebExceptionStatus.RequestCanceled).ConfigureAwait (false);
				}

				using (var stream = response.GetResponseStream ()) {
					switch (TestRunner.EffectiveType) {
					case HttpInstrumentationTestType.ReuseAfterPartialRead:
						content = await ReadStringAsBuffer (stream, 1234).ConfigureAwait (false);
						break;

					case HttpInstrumentationTestType.TestResponseStream:
						content = await TestResponseStream (stream).ConfigureAwait (false);
						break;

					case HttpInstrumentationTestType.LargeChunkRead:
						content = await LargeChunkRead (stream).ConfigureAwait (false);
						break;

					case HttpInstrumentationTestType.LargeGZipRead:
						content = await ReadAsString (stream).ConfigureAwait (false);
						break;

					case HttpInstrumentationTestType.GZipWithLength:
						content = await GZipWithLength (stream).ConfigureAwait (false);
						break;

					case HttpInstrumentationTestType.ResponseStreamCheckLength2:
						content = await ResponseStreamCheckLength (stream, true).ConfigureAwait (false);
						break;

					case HttpInstrumentationTestType.ResponseStreamCheckLength:
						content = await ResponseStreamCheckLength (stream, false).ConfigureAwait (false);
						break;

					case HttpInstrumentationTestType.GetNoLength:
						content = await GetNoLength (stream).ConfigureAwait (false);
						break;

					default:
						content = await ReadAsString (stream).ConfigureAwait (false);
						break;
					}
				}

				var status = response.StatusCode;

				response.Dispose ();
				finishedTcs.TrySetResult (true);
				return new SimpleResponse (this, status, content, error);

				async Task<Response> ReadWithTimeout (int timeout, WebExceptionStatus expectedStatus)
				{
					StreamReader reader = null;
					try {
						reader = new StreamReader (response.GetResponseStream ());
						var readTask = reader.ReadToEndAsync ();
						if (timeout > 0) {
							var timeoutTask = Task.Delay (timeout);
							var task = await Task.WhenAny (timeoutTask, readTask).ConfigureAwait (false);
							if (task == timeoutTask)
								throw ctx.AssertFail ("Timeout expired.");
						}
						var ret = await readTask.ConfigureAwait (false);
						ctx.LogMessage ($"EXPECTED ERROR: {ret}");
						throw ctx.AssertFail ("Expected exception.");
					} catch (WebException wexc) {
						ctx.Assert ((WebExceptionStatus)wexc.Status, Is.EqualTo (expectedStatus));
						return new SimpleResponse (this, HttpStatusCode.InternalServerError, null, wexc);
					} finally {
						finishedTcs.TrySetResult (true);
					}
				}

				async Task<HttpContent> ReadStringAsBuffer (Stream stream, int size)
				{
					var buffer = new byte[size];
					var ret = await stream.ReadAsync (buffer, 0, buffer.Length).ConfigureAwait (false);
					ctx.Assert (ret, Is.EqualTo (buffer.Length));
					return StringContent.CreateMaybeNull (new ASCIIEncoding ().GetString (buffer, 0, ret));
				}

				async Task<HttpContent> ReadAsString (Stream stream)
				{
					using (var reader = new StreamReader (stream)) {
						string text = null;
						if (!reader.EndOfStream)
							text = await reader.ReadToEndAsync ().ConfigureAwait (false);
						return StringContent.CreateMaybeNull (text);
					}
				}

				async Task<HttpContent> TestResponseStream (Stream stream)
				{
					var buffer = new byte[5];
					var ret = await stream.ReadAsync (buffer, 4, 1).ConfigureAwait (false);
					ctx.Assert (ret, Is.EqualTo (1), "#A1");
					ctx.Assert (buffer[4], Is.EqualTo ((byte)65), "#A2");
					ret = await stream.ReadAsync (buffer, 0, 2);
					ctx.Assert (ret, Is.EqualTo (2), "#B1");
					return Handler.Content;
				}

				async Task<HttpContent> LargeChunkRead (Stream stream)
				{
					var buffer = new byte[43];
					var ret = await stream.ReadAsync (buffer, 0, buffer.Length).ConfigureAwait (false);
					ctx.Assert (ret, Is.EqualTo (ConnectionHandler.TheQuickBrownFox.Length), "#A1");
					var text = Encoding.UTF8.GetString (buffer, 0, ret);
					return new StringContent (text);
				}

				async Task<HttpContent> GZipWithLength (Stream stream)
				{
					using (var ms = new MemoryStream ()) {
						await stream.CopyToAsync (ms, 16384).ConfigureAwait (false);
						var bytes = ms.ToArray ();
						var text = Encoding.UTF8.GetString (bytes, 0, bytes.Length);
						return new StringContent (text);
					}
				}

				async Task<HttpContent> ResponseStreamCheckLength (Stream stream, bool chunked)
				{
					await ctx.AssertException<NotSupportedException> (() => Task.FromResult (stream.Length), "Length should throw");
					if (chunked) {
						ctx.Assert (response.ContentLength, Is.EqualTo (-1L), "ContentLength");
						ctx.Assert (response.Headers["Transfer-Encoding"], Is.EqualTo ("chunked"), "chunked encoding");
					} else {
						ctx.Assert (response.ContentLength, Is.EqualTo ((long)Handler.Content.Length), "ContentLength");
						ctx.Assert (response.Headers["Content-Length"], Is.EqualTo (Handler.Content.Length.ToString ()), "Content-Length header");
					}
					return await GZipWithLength (stream).ConfigureAwait (false);
				}

				async Task<HttpContent> GetNoLength (Stream stream)
				{
					ctx.Assert (response.ContentLength, Is.EqualTo (-1L), "ContentLength");
					ctx.Assert (response.Headers["Content-Length"], Is.Null, "No Content-Length: header");
					return await ReadAsString (stream);
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

			public string ME {
				get;
			}

			public HttpInstrumentationContent (HttpInstrumentationTestRunner runner, HttpInstrumentationRequest request)
			{
				TestRunner = runner;
				Request = request;
				ME = $"{GetType ().Name}({runner.EffectiveType})";

				switch (runner.EffectiveType) {
				case HttpInstrumentationTestType.EntityTooBig:
				case HttpInstrumentationTestType.ClientAbortsPost:
					HasLength = true;
					Length = 16;
					break;
				case HttpInstrumentationTestType.PostContentLength:
					HasLength = true;
					Length = ConnectionHandler.TheQuickBrownFoxBuffer.Length;
					break;
				case HttpInstrumentationTestType.NtlmWhileQueued2:
					HasLength = true;
					Length = ((HelloWorldHandler)request.Handler.Target).Message.Length;
					break;
				case HttpInstrumentationTestType.LargeChunkRead:
					break;
				case HttpInstrumentationTestType.GetNoLength:
					NoLength = true;
					break;
				default:
					HasLength = true;
					Length = 4096;
					break;
				}
			}

			public sealed override bool HasLength {
				get;
			}

			public sealed override int Length {
				get;
			}

			public bool NoLength {
				get;
			}

			public override void AddHeadersTo (HttpMessage message)
			{
				if (NoLength) {
					message.ContentType = "text/plain";
				} else if (HasLength) {
					message.ContentType = "text/plain";
					message.ContentLength = Length;
				} else {
					message.TransferEncoding = "chunked";
				}
			}

			public override byte[] AsByteArray ()
			{
				throw new NotImplementedException ();
			}

			public override string AsString ()
			{
				throw new NotImplementedException ();
			}

			public override async Task WriteToAsync (TestContext ctx, Stream stream, CancellationToken cancellationToken)
			{
				ctx.LogDebug (4, $"{ME} WRITE BODY");

				switch (TestRunner.EffectiveType) {
				case HttpInstrumentationTestType.NtlmWhileQueued:
					await HandleNtlmWhileQueued ().ConfigureAwait (false);
					break;

				case HttpInstrumentationTestType.NtlmWhileQueued2:
					await HandleNtlmWhileQueued2 ().ConfigureAwait (false);
					break;

				case HttpInstrumentationTestType.ReadTimeout:
					await stream.WriteAsync (ConnectionHandler.TheQuickBrownFoxBuffer, cancellationToken).ConfigureAwait (false);
					await stream.FlushAsync (cancellationToken);
					await Task.WhenAny (Request.WaitForCompletion (), Task.Delay (10000));
					break;

				case HttpInstrumentationTestType.AbortResponse:
					await stream.WriteAsync (ConnectionHandler.TheQuickBrownFoxBuffer, cancellationToken).ConfigureAwait (false);
					await stream.FlushAsync (cancellationToken);
					await Task.Delay (500).ConfigureAwait (false);
					TestRunner.AbortPrimaryRequest ();
					await Task.WhenAny (Request.WaitForCompletion (), Task.Delay (10000));
					break;

				case HttpInstrumentationTestType.PostChunked:
					await HandlePostChunked ().ConfigureAwait (false);
					break;

				case HttpInstrumentationTestType.EntityTooBig:
					await ctx.AssertException<ProtocolViolationException> (() =>
						stream.WriteAsync (ConnectionHandler.TheQuickBrownFoxBuffer, cancellationToken),
						"writing too many bytes").ConfigureAwait (false);
					break;

				case HttpInstrumentationTestType.PostContentLength:
					await stream.WriteAsync (ConnectionHandler.TheQuickBrownFoxBuffer, cancellationToken).ConfigureAwait (false);
					await stream.FlushAsync (cancellationToken);
					break;

				case HttpInstrumentationTestType.LargeChunkRead:
					await HandleLargeChunkRead ().ConfigureAwait (false);
					break;

				case HttpInstrumentationTestType.GetNoLength:
					await stream.WriteAsync (ConnectionHandler.TheQuickBrownFoxBuffer, cancellationToken).ConfigureAwait (false);
					await stream.FlushAsync (cancellationToken);
					stream.Dispose ();
					break;

				default:
					throw ctx.AssertFail (TestRunner.EffectiveType);
				}

				async Task HandleNtlmWhileQueued ()
				{
					/*
					 * HandleNtlmWhileQueued and HandleNtlmWhileQueued2 don't work on .NET because they
					 * don't do the "priority request" mechanic to send the NTLM challenge before processing
					 * any queued requests.
					 */

					/*
					 * This test is tricky.  We set ServicePoint.ConnectionLimit to 1, then start an NTLM
					 * request.  Using the instrumentation's read handler, we then start another simple
					 * "Hello World" GET request, which will then be queued by the ServicePoint.
					 * 
					 * Once we got to this point, the client did the full NTLM authentication and we are about
					 * to return the final response body.
					 * 
					 * Now we start listening for a new connection by calling StartDelayedListener().
					 * 
					 */
					await Task.Delay (500).ConfigureAwait (false);
					ctx.LogDebug (4, $"{ME} WRITE BODY - ABORT!");

					await TestRunner.StartDelayedSecondaryOperation (ctx);

					/*
					 * Then we abort the client-side NTLM request and wait for it to complete.
					 * This will eventually close the connection, so the ServicePoint scheduler will
					 * start the "Hello World" request.
					 */

					TestRunner.AbortPrimaryRequest ();
					await Task.WhenAny (Request.WaitForCompletion (), Task.Delay (10000));
				}

				async Task HandleNtlmWhileQueued2 ()
				{
					/*
					 * Similar to NtlmWhileQueued, but we now complete both requests.
					 */
					await Task.Delay (500).ConfigureAwait (false);
					await TestRunner.StartDelayedSecondaryOperation (ctx);

					var message = ((HelloWorldHandler)Request.Handler.Target).Message;
					await stream.WriteAsync (message, cancellationToken).ConfigureAwait (false);
					await stream.FlushAsync (cancellationToken);
				}

				async Task HandlePostChunked ()
				{
					await stream.WriteAsync (ConnectionHandler.TheQuickBrownFoxBuffer, cancellationToken).ConfigureAwait (false);
					await stream.FlushAsync (cancellationToken);

					await AbstractConnection.WaitWithTimeout (ctx, 1500, Request.Handler.WaitUntilReady ());

					await stream.WriteAsync (ConnectionHandler.GetLargeTextBuffer (50), cancellationToken);
				}

				async Task HandleLargeChunkRead ()
				{
					await ChunkedContent.WriteChunkAsBlob (
						stream, ConnectionHandler.TheQuickBrownFoxBuffer,
						cancellationToken).ConfigureAwait (false);
					await stream.FlushAsync (cancellationToken);

					await ChunkedContent.WriteChunkAsBlob (
						stream, ConnectionHandler.GetLargeTextBuffer (50),
						cancellationToken);
					await ChunkedContent.WriteChunkTrailer (stream, cancellationToken);
					await stream.FlushAsync (cancellationToken);
				}
			}
		}

		class HttpInstrumentationResponse : Response
		{
			public HttpInstrumentationTestRunner TestRunner {
				get;
			}

			public HttpWebResponse Response {
				get;
			}

			public string ME {
				get;
			}

			TaskCompletionSource<bool> finishedTcs;

			public Task WaitForCompletion ()
			{
				return finishedTcs.Task;
			}

			public HttpInstrumentationResponse (HttpInstrumentationRequest request, HttpWebResponse response)
				: base (request)
			{
				TestRunner = request.TestRunner;
				Response = response;
				finishedTcs = new TaskCompletionSource<bool> ();
				ME = $"{GetType ().Name}({TestRunner.EffectiveType})";
			}

			public HttpInstrumentationResponse (HttpInstrumentationRequest request, WebException error)
				: base (request)
			{
				TestRunner = request.TestRunner;
				Response = (HttpWebResponse)error.Response;
				Error = error;
				finishedTcs = new TaskCompletionSource<bool> ();
				ME = $"{GetType ().Name}({TestRunner.EffectiveType})";
			}

			public override bool IsSuccess => false;

			public override HttpStatusCode Status => Response.StatusCode;

			public sealed override Exception Error {
				get;
			}

			public override HttpContent Content => null;

			internal async Task CheckResponse (TestContext ctx, CancellationToken cancellationToken)
			{
				ctx.LogMessage ("CHECK RESPONSE");
				cancellationToken.ThrowIfCancellationRequested ();

				var stream = Response.GetResponseStream ();

				var buffer = new byte[1024];
				var ret = await stream.ReadAsync (buffer, 0, buffer.Length, cancellationToken).ConfigureAwait (false);

				ctx.LogMessage ($"CHECK RESPONSE #1: {ret}");
			}
		}


	}
}
