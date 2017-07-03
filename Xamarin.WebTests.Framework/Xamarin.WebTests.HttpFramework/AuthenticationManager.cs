//
// AuthenticationManager.cs
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
using System.Net;
using Xamarin.AsyncTests;
using Xamarin.AsyncTests.Constraints;

namespace Xamarin.WebTests.HttpFramework
{
	using Server;
	using HttpHandlers;

	public enum AuthenticationState
	{
		None,
		Unauthenticated,
		Challenge,
		Authenticated,
		Error
	}

	public class AuthenticationManager
	{
		public AuthenticationType AuthenticationType {
			get;
			private set;
		}

		public bool IsProxy {
			get;
		}

		public ICredentials Credentials {
			get;
		}

		string RequestAuthHeader => IsProxy ? "Proxy-Authorization" : "Authorization";
		string ResponseAuthHeader => IsProxy ? "Proxy-Authenticate" : "WWW-Authenticate";
		HttpStatusCode UnauthorizedStatus => IsProxy ? HttpStatusCode.ProxyAuthenticationRequired : HttpStatusCode.Unauthorized;

		public AuthenticationManager (AuthenticationType type, bool isProxy)
		{
			AuthenticationType = type;
			IsProxy = isProxy;
		}

		public AuthenticationManager (AuthenticationType type, ICredentials credentials)
		{
			AuthenticationType = type;
			Credentials = credentials;
		}

		bool haveChallenge;
		bool complete;

		HttpResponse OnError (string format, params object[] args)
		{
			return OnError (string.Format (format, args));
		}

		protected virtual HttpResponse OnError (string message)
		{
			return HttpResponse.CreateError (message);
		}

		HttpResponse OnUnauthenticated (TestContext ctx, HttpConnection connection, HttpRequest request, string token)
		{
			var response = new HttpResponse (UnauthorizedStatus);
			response.AddHeader (ResponseAuthHeader, token);
			return response;
		}

		public void ConfigureRequest (Request request)
		{
			if (Credentials != null)
				request.SetCredentials (Credentials);
		}

		public HttpResponse HandleAuthentication (TestContext ctx, HttpConnection connection, HttpRequest request,
		                                          out AuthenticationState state)
		{
			string authHeader;
			if (!request.Headers.TryGetValue (RequestAuthHeader, out authHeader))
				authHeader = null;

			if (AuthenticationType == AuthenticationType.ForceNone) {
				// Must not contain any auth header
				if (authHeader == null) {
					state = AuthenticationState.None;
					return null;
				}
				state = AuthenticationState.Error;
				return OnError ("Must not contain any auth header.");
			}

			if (request.Method == "GET" || request.Method == "HEAD" || request.Body == null) {
				if (request.Headers.TryGetValue ("Transfer-Encoding", out string transferEncoding))
					ctx.AssertFail ($"Must not send 'Transfer-Encoding' header with '${request.Method}' request.");
				if (request.Headers.TryGetValue ("Content-Length", out string contentLength))
					ctx.AssertFail ($"Must not send 'Content-Length' header with '${request.Method}' request.");
			} else if (haveChallenge && !complete) {
				if (request.Headers.TryGetValue ("Content-Length", out string contentLength))
					ctx.Assert (int.Parse (contentLength), Is.EqualTo (0), "Must send zero-length content with NTLM challenge.");
				else
					ctx.AssertFail ("Must send 'Content-Length: 0' with NTLM challenge.");
			} else {
				if (request.Headers.TryGetValue ("Transfer-Encoding", out string transferEncoding))
					ctx.Assert (transferEncoding, Is.EqualTo ("chunked"), "Transfer-Encoding");
				else if (!request.Headers.TryGetValue ("Content-Length", out string contentLength))
					ctx.AssertFail ("Need either 'Transfer-Encoding' or 'Content-Length'");
			}

			if (authHeader == null) {
				haveChallenge = false;
				state = AuthenticationState.Unauthenticated;
				return OnUnauthenticated (ctx, connection, request, AuthenticationType.ToString ());
			}

			int pos = authHeader.IndexOf (' ');
			var mode = authHeader.Substring (0, pos);
			var arg = authHeader.Substring (pos + 1);

			if (!mode.Equals (AuthenticationType.ToString ())) {
				state = AuthenticationState.Error;
				return OnError ("Invalid authentication scheme: {0}", mode);
			}

			if (mode.Equals ("Basic")) {
				if (arg.Equals ("eGFtYXJpbjptb25rZXk=")) {
					state = AuthenticationState.Authenticated;
					return null;
				}
				state = AuthenticationState.Error;
				return OnError ("Invalid Basic Authentication header");
			} else if (!mode.Equals ("NTLM")) {
				state = AuthenticationState.Error;
				return OnError ("Invalid authentication scheme: {0}", mode);
			}

			var bytes = Convert.FromBase64String (arg);

			if (!DependencyInjector.IsAvailable (typeof (NTLMHandler))) {
				state = AuthenticationState.Error;
				return OnError ("NTLM Support not available.");
			}

			var handler = DependencyInjector.Get<NTLMHandler> ();
			if (handler.HandleNTLM (ref bytes, ref haveChallenge)) {
				state = AuthenticationState.Authenticated;
				return null;
			}

			if (haveChallenge)
				complete = true;

			state = AuthenticationState.Challenge;

			var token = "NTLM " + Convert.ToBase64String (bytes);
			return OnUnauthenticated (ctx, connection, request, token);
		}
	}
}

