﻿//
// TestInstance.cs
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
using System.Xml.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Xamarin.AsyncTests.Framework
{
	abstract class TestInstance
	{
		public TestHost Host {
			get;
		}

		public TestInstance Parent {
			get;
		}

		public TestPath Path {
			get;
		}

		public TestNodeInternal Node {
			get;
		}

		protected TestInstance (TestHost host, TestPath path, TestNodeInternal node, TestInstance parent)
		{
			if (host == null)
				throw new ArgumentNullException ("host");
			if (path == null)
				throw new ArgumentNullException ("path");
			if (node == null)
				throw new ArgumentNullException ("node");

			Host = host;
			Path = path;
			Node = node;
			Parent = parent;
		}

		internal abstract TestParameterValue GetCurrentParameter ();

		protected FixtureTestInstance GetFixtureInstance ()
		{
			TestInstance instance = this;
			while (instance != null) {
				var fixtureInstance = instance as FixtureTestInstance;
				if (fixtureInstance != null)
					return fixtureInstance;

				instance = instance.Parent;
			}

			throw new InternalErrorException ();
		}

		public virtual void Initialize (TestContext ctx)
		{
		}

		public virtual void Destroy (TestContext ctx)
		{
		}

		public TestPath GetCurrentPath ()
		{
			var parameter = GetCurrentParameter ();
			if (parameter == null)
				return Path;

			return parameter.GetCurrentPath ();
		}

		public virtual bool ParameterMatches<T> (string name)
		{
			return Node.ParameterMatches<T> (name);
		}

		public virtual T GetParameter<T> ()
		{
			var parameter = GetCurrentParameter ();
			if (parameter == null)
				throw new InternalErrorException ();

			var node = new TestNodeInternal (Parent.Host, parameter.Parameter);
			return node.GetParameter<T> ();
		}

		public override string ToString ()
		{
			return string.Format ("[{0}: Host={1}, Parent={2}]", GetType ().Name, Host, Parent);
		}
	}
}

