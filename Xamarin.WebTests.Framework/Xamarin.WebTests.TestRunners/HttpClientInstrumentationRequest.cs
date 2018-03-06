﻿//
// HttpClientInstrumentationRequest.cs
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
using System.Threading;
using System.Threading.Tasks;
using System.Collections;
using System.Collections.Generic;
using Xamarin.AsyncTests;
using Xamarin.AsyncTests.Constraints;
using Xamarin.AsyncTests.Portable;

namespace Xamarin.WebTests.TestRunners
{
	using HttpFramework;
	using HttpHandlers;
	using HttpClient;

	class HttpClientInstrumentationRequest : Request
	{
		public InstrumentationOperation Operation {
			get;
		}

		public HttpClientInstrumentationHandler Parent {
			get;
		}

		public Uri RequestUri {
			get;
		}

		protected IHttpClientProvider Provider {
			get;
		}

		protected IHttpClient Client {
			get;
		}

		protected IHttpClientHandler Handler {
			get;
		}

		public HttpClientInstrumentationRequest (
			InstrumentationOperation operation,
			HttpClientInstrumentationHandler handler,
			Uri requestUri)
		{
			Operation = operation;
			Parent = handler;
			RequestUri = requestUri;

			Provider = DependencyInjector.Get<IHttpClientProvider> ();
			Handler = Provider.Create ();
			Client = Handler.CreateHttpClient ();
		}

		public HttpClientInstrumentationRequest (
			InstrumentationOperation operation,
			HttpClientInstrumentationHandler handler,
			HttpClientInstrumentationRequest primaryRequest,
			Uri requestUri)
		{
			Operation = operation;
			Parent = handler;
			RequestUri = requestUri;

			Handler = primaryRequest.Handler;
			Client = primaryRequest.Client;
		}

		public override string Method {
			get => throw new NotSupportedException ();
			set => throw new NotSupportedException ();
		}

		public override void Abort ()
		{
			Client.CancelPendingRequests ();
		}

		public void ConfigureRequest (TestContext ctx, Uri uri)
		{
			switch (Parent.TestRunner.EffectiveType) {
			case HttpClientTestType.SimpleGZip:
			case HttpClientTestType.ParallelGZip:
			case HttpClientTestType.ParallelGZipNoClose:
			case HttpClientTestType.SequentialGZip:
				Handler.AutomaticDecompression = true;
				break;
			case HttpClientTestType.ReuseHandlerGZip:
				if (Operation.Type == InstrumentationOperationType.Primary)
					Handler.AutomaticDecompression = true;
				break;
			case HttpClientTestType.SequentialRequests:
			case HttpClientTestType.SequentialChunked:
			case HttpClientTestType.ReuseHandler:
			case HttpClientTestType.ReuseHandlerNoClose:
			case HttpClientTestType.ReuseHandlerChunked:
				break;
			default:
				throw ctx.AssertFail (Parent.TestRunner.EffectiveType);
			}
		}

		public override Task<Response> SendAsync (TestContext ctx, CancellationToken cancellationToken)
		{
			switch (Parent.TestRunner.EffectiveType) {
			case HttpClientTestType.SimpleGZip:
			case HttpClientTestType.ParallelGZip:
			case HttpClientTestType.ReuseHandler:
				return GetString (ctx, cancellationToken);
			case HttpClientTestType.ParallelGZipNoClose:
			case HttpClientTestType.SequentialRequests:
			case HttpClientTestType.SequentialChunked:
			case HttpClientTestType.SequentialGZip:
			case HttpClientTestType.ReuseHandlerNoClose:
			case HttpClientTestType.ReuseHandlerChunked:
			case HttpClientTestType.ReuseHandlerGZip:
				return GetStringNoClose (ctx, cancellationToken);
			default:
				throw ctx.AssertFail (Parent.TestRunner.EffectiveType);
			}
		}

		public override void SendChunked ()
		{
			throw new NotSupportedException ();
		}

		public override void SetContentLength (long contentLength)
		{
			throw new NotSupportedException ();
		}

		public override void SetContentType (string contentType)
		{
			throw new NotSupportedException ();
		}

		public override void SetCredentials (ICredentials credentials)
		{
			throw new NotSupportedException ();
		}

		public override void SetProxy (IWebProxy proxy)
		{
			if (proxy != null)
				throw new NotSupportedException ();
		}

		async Task<Response> GetString (TestContext ctx, CancellationToken cancellationToken)
		{
			try {
				var body = await Client.GetStringAsync (RequestUri);
				return new SimpleResponse (this, HttpStatusCode.OK, StringContent.CreateMaybeNull (body));
			} catch (Exception ex) {
				return new SimpleResponse (this, HttpStatusCode.InternalServerError, null, ex);
			}
		}

		async Task<Response> GetStringNoClose (TestContext ctx, CancellationToken cancellationToken)
		{
			var method = Handler.CreateRequestMessage (HttpMethod.Get, RequestUri);
			method.SetKeepAlive ();
			var response = await Client.SendAsync (
				method, HttpCompletionOption.ResponseContentRead, cancellationToken).ConfigureAwait (false);
			ctx.Assert (response.IsSuccessStatusCode, "success");
			var content = await response.Content.ReadAsStringAsync ();
			return new SimpleResponse (this, HttpStatusCode.OK, StringContent.CreateMaybeNull (content));
		}
	}
}
