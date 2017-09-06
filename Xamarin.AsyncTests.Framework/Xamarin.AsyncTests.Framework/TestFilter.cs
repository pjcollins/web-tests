﻿//
// TestFilter.cs
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
using System.Linq;
using System.Collections.Generic;

namespace Xamarin.AsyncTests.Framework
{
	class TestFilter
	{
		readonly TestFilter parent;
		readonly IList<TestCategoryAttribute> categories;
		readonly IEnumerable<TestFeature> features;

		public TestFilter (TestFilter parent, IList<TestCategoryAttribute> categories, IEnumerable<TestFeature> features)
		{
			this.parent = parent;
			this.categories = categories;
			this.features = features;
		}

		public bool MustMatch {
			get { return parent != null; }
		}

		public bool Filter (TestContext ctx, out bool enabled)
		{
			if (categories.Any (attr => attr.Category == TestCategory.Global)) {
				enabled = true;
				return true;
			}

			foreach (var feature in features) {
				if (!ctx.IsEnabled (feature)) {
					enabled = false;
					return true;
				}
			}

			foreach (var attr in categories) {
				if (attr is MartinAttribute martin) {
					if (ctx.CurrentCategory != TestCategory.Martin) {
						enabled = false;
						return true;
					}
					if (string.IsNullOrEmpty (ctx.Settings.MartinTest) ||
					    string.Equals (ctx.Settings.MartinTest, "all", StringComparison.OrdinalIgnoreCase) ||
					    string.Equals (ctx.Settings.MartinTest, martin.Parameter, StringComparison.OrdinalIgnoreCase)) {
						enabled = true;
						return true;
					}
					enabled = false;
					return true;
				}
				if (ctx.CurrentCategory == attr.Category) {
					enabled = true;
					return true;
				}
				if (attr.Category.IsExplicit) {
					enabled = false;
					return true;
				}
			}

			if (parent != null && parent.Filter (ctx, out enabled))
				return enabled;

			if (ctx.CurrentCategory == TestCategory.All) {
				enabled = true;
				return true;
			}

			enabled = false;
			return false;
		}
	}
}

