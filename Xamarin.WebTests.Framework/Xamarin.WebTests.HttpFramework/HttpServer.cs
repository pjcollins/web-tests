﻿//
// ServerInstance.cs
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
using System.IO;
using System.Text;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using System.Collections;
using System.Collections.Generic;

using Xamarin.AsyncTests;
using Xamarin.AsyncTests.Constraints;
using Xamarin.AsyncTests.Portable;
using Xamarin.AsyncTests.Framework;

namespace Xamarin.WebTests.HttpFramework
{
	using ConnectionFramework;
	using HttpHandlers;
	using Server;

	[FriendlyName ("[HttpServer]")]
	public class HttpServer : ITestInstance
	{
		public HttpBackend Backend {
			get;
		}

		int countRequests;

		static long nextId;
		Dictionary<string,Handler> handlers = new Dictionary<string, Handler> ();

		public HttpServer (HttpBackend backend)
		{
			Backend = backend;
		}

		public virtual Uri Uri {
			get { return Backend.Uri; }
		}

		public bool ReuseConnection {
			get { return (Backend.Flags & ListenerFlags.ReuseConnection) != 0; }
		}

		#region ITestInstance implementation

		bool initialized;

		public async Task Initialize (TestContext ctx, CancellationToken cancellationToken)
		{
			if (initialized)
				throw new InvalidOperationException ();
			initialized = true;

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
			if (!initialized)
				throw new InvalidOperationException ();

			if (ReuseConnection)
				await Stop (ctx, cancellationToken);

			initialized = false;
		}

		#endregion

		public Task Start (TestContext ctx, CancellationToken cancellationToken)
		{
			return Backend.Start (ctx, this, cancellationToken);
		}

		public Task Stop (TestContext ctx, CancellationToken cancellationToken)
		{
			return Backend.Stop (ctx, this, cancellationToken);
		}

		public int CountRequests {
			get { return countRequests; }
		}

		public Uri RegisterHandler (Handler handler)
		{
			var path = string.Format ("/{0}/{1}/", handler.GetType (), ++nextId);
			handlers.Add (path, handler);
			return new Uri (Uri, path);
		}

		public void RegisterHandler (string path, Handler handler)
		{
			handlers.Add (path, handler);
		}

		public virtual HttpConnection CreateConnection (TestContext ctx, Stream stream)
		{
			return Backend.CreateConnection (ctx, this, stream);
		}

		protected virtual void OnHandleConnection (TestContext ctx, HttpConnection connection, HttpRequest request)
		{
		}

		public bool HandleConnection (TestContext ctx, HttpConnection connection, HttpRequest request)
		{
			++countRequests;
			OnHandleConnection (ctx, connection, request);

			var path = request.Path;
			var handler = handlers [path];
			handlers.Remove (path);

			return handler.HandleRequest (connection, request);
		}

		protected void Debug (TestContext ctx, int level, Handler handler, string message, params object[] args)
		{
			var sb = new StringBuilder ();
			sb.AppendFormat ("{0}:{1}: {2}", this, handler, message);
			for (int i = 0; i < args.Length; i++) {
				sb.Append (" ");
				sb.Append (args [i] != null ? args [i].ToString () : "<null>");
			}

			ctx.LogDebug (level, sb.ToString ());
		}

		protected virtual string MyToString ()
		{
			var sb = new StringBuilder ();
			if (ReuseConnection)
				sb.Append ("shared");
			if (Backend.UseSSL) {
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

