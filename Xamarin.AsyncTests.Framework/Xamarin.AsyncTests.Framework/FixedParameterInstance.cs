﻿//
// FixedParameterInstance.cs
//
// Author:
//       Martin Baulig <martin.baulig@xamarin.com>
//
// Copyright (c) 2015 Xamarin, Inc.
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

namespace Xamarin.AsyncTests.Framework
{
	class FixedParameterInstance<T> : ParameterizedTestInstance
	{
		new public FixedParameterHost<T> Host {
			get { return (FixedParameterHost<T>)base.Host; }
		}

		public FixedParameterInstance (FixedParameterHost<T> host, TestPath path, TestNodeInternal node, TestInstance parent)
			: base (host, path, node, parent)
		{
		}

		public override void Initialize (TestContext ctx)
		{
			hasNext = true;
			fixedValue = new FixedValue (this, Host.GetFixedParameter (), Host.Attribute.Value);
			base.Initialize (ctx);
		}

		public override void Destroy (TestContext ctx)
		{
			hasNext = false;
			fixedValue = null;
			base.Destroy (ctx);
		}

		bool hasNext;
		ParameterizedTestValue fixedValue;

		public override bool HasNext ()
		{
			return hasNext;
		}

		public override bool MoveNext (TestContext ctx)
		{
			if (!hasNext)
				return false;
			hasNext = false;
			return true;
		}

		public override ParameterizedTestValue Current {
			get { return fixedValue; }
		}

		class FixedValue : ParameterizedTestValue
		{
			new public FixedParameterInstance<T> Instance {
				get { return (FixedParameterInstance<T>)base.Instance;  }
			}

			readonly ITestParameter parameter;

			public FixedValue (FixedParameterInstance<T> instance, ITestParameter parameter, object value)
				: base (instance, value)
			{
				this.parameter = parameter;
			}

			public override ITestParameter Parameter {
				get {
					return parameter;
				}
			}
		}
	}
}

