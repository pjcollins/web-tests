﻿﻿//
// HttpServer.cs
//
// Author:
//       Martin Baulig <martin.baulig@xamarin.com>
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
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Xamarin.AsyncTests;
using Xamarin.AsyncTests.Portable;
using Xamarin.AsyncTests.Constraints;
using Xamarin.WebTests.ConnectionFramework;
using Xamarin.WebTests.TestFramework;
using Xamarin.WebTests.HttpHandlers;
using Xamarin.WebTests.Server;

namespace Xamarin.WebTests.HttpFramework {
	[HttpServer]
	[FriendlyName ("HttpServer")]
	public abstract class HttpServer : ITestInstance {
		public abstract HttpServerFlags Flags {
			get;
		}

		public abstract bool UseSSL {
			get;
		}

		public abstract Uri Uri {
			get;
		}

		public abstract Uri TargetUri {
			get;
		}

		public IHttpServerDelegate Delegate {
			get; set;
		}

		public abstract IPortableEndPoint ListenAddress {
			get;
		}

		public abstract IWebProxy GetProxy ();

		public bool ReuseConnection {
			get { return (Flags & HttpServerFlags.ReuseConnection) != 0; }
		}

		#region ITestInstance implementation

		int initialized;

		public async Task Initialize (TestContext ctx, CancellationToken cancellationToken)
		{
			if (Interlocked.CompareExchange (ref initialized, 1, 0) != 0)
				throw new InternalErrorException ();

			if (ReuseConnection)
				await Start (ctx, cancellationToken);
		}

		public async Task PreRun (TestContext ctx, CancellationToken cancellationToken)
		{
			if (!ReuseConnection)
				await Start (ctx, cancellationToken);
		}

		public async Task PostRun (TestContext ctx, CancellationToken cancellationToken)
		{
			if (!ReuseConnection)
				await Stop (ctx, cancellationToken);
		}

		public async Task Destroy (TestContext ctx, CancellationToken cancellationToken)
		{
			if (ReuseConnection)
				await Stop (ctx, cancellationToken);

			Interlocked.Exchange (ref initialized, 0);
		}

		public abstract Task Start (TestContext ctx, CancellationToken cancellationToken);

		public abstract Task Stop (TestContext ctx, CancellationToken cancellationToken);

		#endregion

		internal async Task<HttpConnection> CreateConnection (TestContext ctx, BuiltinListenerContext context, CancellationToken cancellationToken)
		{
			++countRequests;
			try {
				var connection = await context.CreateConnection (ctx, cancellationToken).ConfigureAwait (false);
				if (Delegate != null && !await Delegate.CheckCreateConnection (ctx, connection, null, cancellationToken))
					return null;
				return connection;
			} catch (Exception error) {
				if (Delegate == null)
					throw;
				await Delegate.CheckCreateConnection (ctx, null, error, cancellationToken);
				return null;
			}
		}

		public int CountRequests => countRequests;

		static long nextId;
		int countRequests;

		public Uri RegisterHandler (Handler handler)
		{
			var path = string.Format ("/{0}/{1}/", handler.GetType (), ++nextId);
			RegisterHandler (path, handler);
			return new Uri (TargetUri, path);
		}

		public abstract void RegisterHandler (string path, Handler handler);

		protected internal abstract Handler GetHandler (string path);

		public async Task<bool> HandleConnection (TestContext ctx, HttpConnection connection,
		                                          HttpRequest request, CancellationToken cancellationToken)
		{
			var handler = GetHandler (request.Path);
			if (Delegate != null && !Delegate.HandleConnection (ctx, connection, request, handler))
				return false;

			return await handler.HandleRequest (connection, request, cancellationToken).ConfigureAwait (false);
		}

		public void CheckEncryption (TestContext ctx, ISslStream sslStream)
		{
			if ((Flags & (HttpServerFlags.SSL | HttpServerFlags.ForceTls12)) == 0)
				return;

			ctx.Assert (sslStream, Is.Not.Null, "Needs SslStream");
			ctx.Assert (sslStream.IsAuthenticated, "Must be authenticated");

			var setup = DependencyInjector.Get<IConnectionFrameworkSetup> ();
			if (((Flags & HttpServerFlags.ForceTls12) != 0) || setup.SupportsTls12)
				ctx.Assert (sslStream.ProtocolVersion, Is.EqualTo (ProtocolVersions.Tls12), "Needs TLS 1.2");
		}

		protected virtual string MyToString ()
		{
			var sb = new StringBuilder ();
			if ((Flags & HttpServerFlags.ReuseConnection) != 0)
				sb.Append ("shared");
			if (UseSSL) {
				if (sb.Length > 0)
					sb.Append (",");
				sb.Append ("ssl");
			}
			return sb.ToString ();
		}

		public override string ToString ()
		{
			var description = MyToString ();
			var padding = string.IsNullOrEmpty (description) ? string.Empty : ": ";
			return string.Format ("[{0}{1}{2}]", GetType ().Name, padding, description);
		}
	}
}
