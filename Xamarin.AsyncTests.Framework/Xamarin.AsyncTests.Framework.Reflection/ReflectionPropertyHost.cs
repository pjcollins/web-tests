﻿//
// ReflectionPropertyHost.cs
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

namespace Xamarin.AsyncTests.Framework.Reflection
{
	class ReflectionPropertyHost : ParameterizedTestHost
	{
		public ReflectionTestFixtureBuilder Fixture {
			get;
			private set;
		}

		public PropertyInfo Property {
			get;
			private set;
		}

		public ParameterizedTestHost Host {
			get;
			private set;
		}

		public ReflectionPropertyHost (ReflectionTestFixtureBuilder fixture,
			PropertyInfo prop, ParameterizedTestHost host)
			: base (prop.Name, prop.PropertyType.GetTypeInfo (), host.Serializer, host.Flags)
		{
			Fixture = fixture;
			Property = prop;
			Host = host;
		}

		internal override TestInstance CreateInstance (TestPathInternal path, TestInstance parent)
		{
			var instance = (ParameterizedTestInstance)Host.CreateInstance (path, parent);
			return new ReflectionPropertyInstance (this, instance, parent);
		}

		class ReflectionPropertyInstance : ParameterizedTestInstance
		{
			new public ReflectionPropertyHost Host {
				get { return (ReflectionPropertyHost)base.Host; }
			}

			public ParameterizedTestInstance Instance {
				get;
				private set;
			}

			ParameterizedTestValue current;

			public ReflectionPropertyInstance (ReflectionPropertyHost host, ParameterizedTestInstance instance, TestInstance parent)
				: base (host, instance.Path, parent)
			{
				Instance = instance;
			}

			public override void Initialize (TestContext ctx)
			{
				Instance.Initialize (ctx);
			}

			public override bool HasNext ()
			{
				return Instance.HasNext ();
			}

			public override bool MoveNext (TestContext ctx)
			{
				if (!Instance.MoveNext (ctx))
					return false;
				current = Instance.Current;

				Host.Property.SetValue (GetFixtureInstance ().Instance, current.Value);
				return true;
			}

			public override ParameterizedTestValue Current {
				get { return current; }
			}
		}
	}
}

