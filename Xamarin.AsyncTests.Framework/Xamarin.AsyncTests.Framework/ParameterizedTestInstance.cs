﻿//
// ParameterizedTestInstance.cs
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
using System.Reflection;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Xamarin.AsyncTests.Framework
{
	abstract class ParameterizedTestInstance : TestInstance
	{
		public new ParameterizedTestHost Host {
			get { return (ParameterizedTestHost)base.Host; }
		}

		public ParameterizedTestInstance (ParameterizedTestHost host, TestPath path, TestNodeInternal node, TestInstance parent)
			: base (host, path, node, parent)
		{
		}

		public abstract ParameterizedTestValue Current {
			[StackTraceEntryPoint]
			get;
		}

		internal ITestParameter Serialize ()
		{
			if (Current == null)
				return null;

			return Current.Parameter;
		}

		internal override TestParameterValue GetCurrentParameter ()
		{
			return Current;
		}

		public override bool ParameterMatches<T> (string name)
		{
			return Node.ParameterMatches<T> (name);
		}

		public override T GetParameter<T> ()
		{
			return (T)Current.Value;
		}

		[StackTraceEntryPoint]
		public abstract bool HasNext ();

		[StackTraceEntryPoint]
		public abstract bool MoveNext (TestContext ctx);
	}
}
