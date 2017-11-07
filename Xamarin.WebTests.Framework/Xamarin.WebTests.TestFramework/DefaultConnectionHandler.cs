﻿//
// DefaultConnectionHandler.cs
//
// Author:
//       Martin Baulig <martin.baulig@xamarin.com>
//
// Copyright (c) 2015 Xamarin Inc. (http://www.xamarin.com)
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
using System.Threading;
using System.Threading.Tasks;
using Xamarin.AsyncTests;
using Xamarin.WebTests.ConnectionFramework;

namespace Xamarin.WebTests.TestFramework
{
	public class DefaultConnectionHandler : ConnectionHandler
	{
		public DefaultConnectionHandler (ClientAndServer runner)
			: base (runner)
		{
		}

		const string TestCompletedString = @"TestCompleted";
		static readonly byte[] TestCompletedBlob = GetTextBuffer (TestCompletedString);

		protected override async Task HandleClientRead (TestContext ctx, CancellationToken cancellationToken)
		{
			await ExpectBlob (ctx, Client, TestCompletedString, TestCompletedBlob, cancellationToken).ConfigureAwait (false);
			StartClientWrite ();
		}

		protected override async Task HandleClientWrite (TestContext ctx, CancellationToken cancellationToken)
		{
			await WriteBlob (ctx, Client, TestCompletedString, TestCompletedBlob, cancellationToken);
		}

		protected override async Task HandleServerRead (TestContext ctx, CancellationToken cancellationToken)
		{
			await ExpectBlob (ctx, Server, TestCompletedString, TestCompletedBlob, cancellationToken);
		}

		protected override async Task HandleServerWrite (TestContext ctx, CancellationToken cancellationToken)
		{
			await WriteBlob (ctx, Server, TestCompletedString, TestCompletedBlob, cancellationToken);
		}

		protected override async Task HandleMainLoop (TestContext ctx, CancellationToken cancellationToken)
		{
			await Client.Stream.FlushAsync ().ConfigureAwait (false);
			await Server.Stream.FlushAsync ();
		}

		protected override Task HandleClient (TestContext ctx, CancellationToken cancellationToken)
		{
			StartClientRead ();
			return FinishedTask;
		}

		protected override Task HandleServer (TestContext ctx, CancellationToken cancellationToken)
		{
			StartServerWrite ();
			StartServerRead ();
			return FinishedTask;
		}
	}
}

