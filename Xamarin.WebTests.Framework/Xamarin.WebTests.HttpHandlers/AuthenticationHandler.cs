//
// AuthenticatedPostHandler.cs
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
using System.Threading;
using System.Threading.Tasks;

using Xamarin.AsyncTests;

namespace Xamarin.WebTests.HttpHandlers
{
	using HttpFramework;
	using Server;

	public class AuthenticationHandler : AbstractRedirectHandler
	{
		bool cloneable;

		public AuthenticationType AuthenticationType {
			get;
		}

		public AuthenticationManager Manager {
			get;
		}

		public AuthenticationHandler (AuthenticationType type, Handler target, string identifier = null)
			: base (target, identifier ?? CreateIdentifier (type, target))
		{
			AuthenticationType = type;
			if ((target.Flags & RequestFlags.KeepAlive) != 0)
				Flags |= RequestFlags.KeepAlive;
			Manager = new AuthenticationManager (AuthenticationType, GetCredentials ());
			cloneable = true;
		}

		AuthenticationHandler (AuthenticationHandler other)
			: base ((Handler)other.Target.Clone (), other.Value)
		{
			AuthenticationType = other.AuthenticationType;
			Flags = other.Flags;
			Manager = new AuthenticationManager (AuthenticationType, GetCredentials ());
			cloneable = true;
		}

		static string CreateIdentifier (AuthenticationType type, Handler target)
		{
			return string.Format ("Authentication({0}): {1}", type, target.Value);
		}

		public override object Clone ()
		{
			if (!cloneable)
				throw new InternalErrorException ();
			return new AuthenticationHandler (this);
		}

		protected internal override async Task<HttpResponse> HandleRequest (
			TestContext ctx, HttpConnection connection, HttpRequest request,
			RequestFlags effectiveFlags, CancellationToken cancellationToken)
		{
			AuthenticationState state;
			var response = Manager.HandleAuthentication (ctx, connection, request, out state);
			if (response != null) {
				connection.Server.RegisterHandler (request.Path, this);
				return response;
			}

			effectiveFlags |= RequestFlags.Redirected;

			cancellationToken.ThrowIfCancellationRequested (); 
			return await Target.HandleRequest (ctx, connection, request, effectiveFlags, cancellationToken);
		}

		public override bool CheckResponse (TestContext ctx, Response response)
		{
			return Target.CheckResponse (ctx, response);
		}

		public override void ConfigureRequest (Request request, Uri uri)
		{
			base.ConfigureRequest (request, uri);
			Manager.ConfigureRequest (request);
		}

		internal static ICredentials GetCredentials ()
		{
			return new NetworkCredential ("xamarin", "monkey");
		}
	}
}

