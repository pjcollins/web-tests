﻿//
// CustomTestHost.cs
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
using System.Xml.Linq;
using System.Reflection;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Xamarin.AsyncTests.Framework
{
	class CustomTestHost<T> : HeavyTestHost
		where T : ITestInstance
	{
		public Type HostType {
			get;
			private set;
		}

		public bool UseFixtureInstance {
			get;
			private set;
		}

		public CustomTestHost (string name, Type hostType, bool useFixtureInstance)
			: base (name, typeof(T))
		{
			HostType = hostType;
			UseFixtureInstance = useFixtureInstance;
		}

		internal override TestInstance CreateInstance (TestInstance parent)
		{
			return new CustomTestInstance<T> (this, parent, HostType, UseFixtureInstance);
		}

		internal override TestInvoker Deserialize (XElement node, TestInvoker invoker)
		{
			return CreateInvoker (invoker);
		}

		public override string ToString ()
		{
			return string.Format ("[CustomTestHost: Type={0}, HostType={1}, UseFixtureInstance={2}]",
				typeof (T).Name, HostType != null ? HostType.Name : "<null>", UseFixtureInstance);
		}
	}
}

