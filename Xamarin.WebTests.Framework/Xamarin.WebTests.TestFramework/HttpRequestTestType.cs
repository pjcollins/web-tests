﻿//
// HttpRequestTestType.cs
//
// Author:
//       Martin Baulig <mabaul@microsoft.com>
//
// Copyright (c) 2018 Xamarin Inc. (http://www.xamarin.com)
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
namespace Xamarin.WebTests.TestFramework
{
	[HttpRequestTestType]
	public enum HttpRequestTestType
	{
		Simple,
		// InvalidDataDuringHandshake,
		// AbortDuringHandshake,
		// ParallelRequests,
		// ThreeParallelRequests,
		// ParallelRequestsSomeQueued,
		// ManyParallelRequests,
		// ManyParallelRequestsStress,
		// SimpleQueuedRequest,
		// CancelQueuedRequest,
		// CancelMainWhileQueued,
		SimpleNtlm,
		// NtlmWhileQueued,
		// NtlmWhileQueued2,
		ReuseConnection,
		SimplePost,
		SimpleRedirect,
		PostRedirect,
		PostNtlm,
		NtlmChunked,
		ReuseConnection2,
		Get404,
		CloseIdleConnection,
		NtlmInstrumentation,
		NtlmClosesConnection,
		NtlmReusesConnection,
		ParallelNtlm,
		LargeHeader,
		LargeHeader2,
		SendResponseAsBlob,
		ReuseAfterPartialRead,
		CustomConnectionGroup,
		ReuseCustomConnectionGroup,
		CloseCustomConnectionGroup,
		CloseRequestStream,
		ReadTimeout,
		// AbortResponse,
		RedirectOnSameConnection,
		RedirectNoReuse,
		RedirectNoLength,
		PutChunked,
		PutChunkDontCloseRequest,
		ServerAbortsRedirect,
		ServerAbortsPost,
		PostChunked,
		EntityTooBig,
		PostContentLength,
		ClientAbortsPost,
		GetChunked,
		SimpleGZip,
		TestResponseStream,
		LargeChunkRead,
		LargeGZipRead,
		GZipWithLength,
		ResponseStreamCheckLength,
		ResponseStreamCheckLength2,
		GetNoLength,

		ImplicitHost,
		CustomHost,
		CustomHostWithPort,
		CustomHostDefaultPort,

		MartinTest
	}
}
