//
// ListenerTask.cs
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
using System.Threading;
using System.Threading.Tasks;

namespace Xamarin.WebTests.Server
{
	abstract class ListenerTask
	{
		public abstract ConnectionState State {
			get;
		}

		public abstract Task Task {
			get;
		}

		public abstract ConnectionState Continue ();

		public static ListenerTask Create<T> (Func<Task<T>> start, Func<T, ConnectionState> continuation)
		{
			return new ListenerTask<T> (start, continuation);
		}

		public static ListenerTask Create<T, U> (Func<Task<(T, U)>> start, Func<T, U, ConnectionState> continuation)
		{
			return new ListenerTask<(T, U)> (start, r => continuation (r.Item1, r.Item2));
		}

		public static ListenerTask Create<T, U, V> (Func<Task<(T, U, V)>> start, Func<T, U, V, ConnectionState> continuation)
		{
			return new ListenerTask<(T, U, V)> (start, r => continuation (r.Item1, r.Item2, r.Item3));
		}
	}

	class ListenerTask<T> : ListenerTask
	{
		public override ConnectionState State {
			get {
				throw new NotImplementedException ();
			}
		}

		public Task<T> Start {
			get;
		}

		public override Task Task => Start;

		public Func<T, ConnectionState> Continuation {
			get;
		}

		public ListenerTask (Func<Task<T>> start, Func<T, ConnectionState> continuation)
		{
			Start = start ();
			Continuation = continuation;
		}

		public override ConnectionState Continue ()
		{
			var result = Start.Result;
			return Continuation (result);
		}
	}
}
