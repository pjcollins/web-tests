﻿//
// IntermediateServerData.cs
//
// Author:
//       Martin Baulig <martin.baulig@xamarin.com>
//
// Copyright (c) 2016 Xamarin Inc. (http://www.xamarin.com)

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

namespace Xamarin.WebTests.Resources
{
	public class IntermediateServerData : CertificateInfo
	{
		public IntermediateServerData (CertificateDataWithKey data)
			: base (data.Type, data)
		{
		}

		const string subject = "/C=US/ST=Massachusetts/O=Xamarin/OU=Engineering/CN=intermediate-server.local";
		const string issuer = IntermediateCAData.subject;
		const string managedSubject = "CN=intermediate-server.local, OU=Engineering, O=Xamarin, S=Massachusetts, C=US";
		const string managedIssuer = IntermediateCAData.managedSubject;

		internal static readonly byte[] hash = new byte[] {
			0xae, 0x3e, 0x31, 0x37, 0x52, 0xef, 0xdb, 0xab, 0x88, 0xcf, 0x78, 0x3c, 0x21, 0x62, 0x42, 0x75,
			0x70, 0x65, 0x5f, 0x82
		};

		internal static readonly byte[] subject_rawData = new byte[] {
			0x30, 0x71, 0x31, 0x0b, 0x30, 0x09, 0x06, 0x03, 0x55, 0x04, 0x06, 0x13, 0x02, 0x55, 0x53, 0x31,
			0x16, 0x30, 0x14, 0x06, 0x03, 0x55, 0x04, 0x08, 0x13, 0x0d, 0x4d, 0x61, 0x73, 0x73, 0x61, 0x63,
			0x68, 0x75, 0x73, 0x65, 0x74, 0x74, 0x73, 0x31, 0x10, 0x30, 0x0e, 0x06, 0x03, 0x55, 0x04, 0x0a,
			0x13, 0x07, 0x58, 0x61, 0x6d, 0x61, 0x72, 0x69, 0x6e, 0x31, 0x14, 0x30, 0x12, 0x06, 0x03, 0x55,
			0x04, 0x0b, 0x13, 0x0b, 0x45, 0x6e, 0x67, 0x69, 0x6e, 0x65, 0x65, 0x72, 0x69, 0x6e, 0x67, 0x31,
			0x22, 0x30, 0x20, 0x06, 0x03, 0x55, 0x04, 0x03, 0x13, 0x19, 0x69, 0x6e, 0x74, 0x65, 0x72, 0x6d,
			0x65, 0x64, 0x69, 0x61, 0x74, 0x65, 0x2d, 0x73, 0x65, 0x72, 0x76, 0x65, 0x72, 0x2e, 0x6c, 0x6f,
			0x63, 0x61, 0x6c
		};
		internal static readonly byte[] subject_rawDataCanon = new byte[] {
			0x31, 0x0b, 0x30, 0x09, 0x06, 0x03, 0x55, 0x04, 0x06, 0x0c, 0x02, 0x75, 0x73, 0x31, 0x16, 0x30,
			0x14, 0x06, 0x03, 0x55, 0x04, 0x08, 0x0c, 0x0d, 0x6d, 0x61, 0x73, 0x73, 0x61, 0x63, 0x68, 0x75,
			0x73, 0x65, 0x74, 0x74, 0x73, 0x31, 0x10, 0x30, 0x0e, 0x06, 0x03, 0x55, 0x04, 0x0a, 0x0c, 0x07,
			0x78, 0x61, 0x6d, 0x61, 0x72, 0x69, 0x6e, 0x31, 0x14, 0x30, 0x12, 0x06, 0x03, 0x55, 0x04, 0x0b,
			0x0c, 0x0b, 0x65, 0x6e, 0x67, 0x69, 0x6e, 0x65, 0x65, 0x72, 0x69, 0x6e, 0x67, 0x31, 0x22, 0x30,
			0x20, 0x06, 0x03, 0x55, 0x04, 0x03, 0x0c, 0x19, 0x69, 0x6e, 0x74, 0x65, 0x72, 0x6d, 0x65, 0x64,
			0x69, 0x61, 0x74, 0x65, 0x2d, 0x73, 0x65, 0x72, 0x76, 0x65, 0x72, 0x2e, 0x6c, 0x6f, 0x63, 0x61,
			0x6c
		};
		internal static readonly byte[] issuer_rawData = IntermediateCAData.subject_rawData;
		internal static readonly byte[] issuer_rawDataCanon = IntermediateCAData.subject_rawDataCanon;

		internal static readonly CertificateNameInfo subjectName = new CertificateNameInfo (
			0x9e741483L, 0x6589ba44L, subject_rawData, subject_rawDataCanon, subject);
		internal static readonly CertificateNameInfo issuerName = IntermediateCAData.subjectName;

		static readonly DateTime notBefore = new DateTime (2016, 8, 15, 17, 04, 32, DateTimeKind.Utc);
		static readonly DateTime notAfter = new DateTime (2026, 8, 13, 17, 04, 32, DateTimeKind.Utc);

		internal static readonly byte[] serial = new byte[] {
			0x10, 0x00, 0x06
		};
		internal static readonly byte[] serialMono = new byte[] {
			0x06, 0x00, 0x10
		};

		internal static readonly byte[] publicKeyData = new byte[] {
			0x30, 0x82, 0x02, 0x0a, 0x02, 0x82, 0x02, 0x01, 0x00, 0xae, 0xe3, 0xa0, 0x93, 0x21, 0x66, 0x38,
			0xb6, 0x27, 0xb7, 0xa0, 0xbd, 0x17, 0x4b, 0xa1, 0xa2, 0xc7, 0xe8, 0x7e, 0x85, 0xe6, 0xc8, 0xdd,
			0x9d, 0x51, 0xf0, 0x15, 0xf9, 0xc7, 0x74, 0xf8, 0x2e, 0x56, 0x4d, 0x90, 0x6c, 0x11, 0x39, 0xf4,
			0xc1, 0x2f, 0x9a, 0xdd, 0x6d, 0xaa, 0xcf, 0x1d, 0x35, 0xc6, 0x43, 0x28, 0x01, 0x2a, 0x90, 0x90,
			0x52, 0xbd, 0xb5, 0x2e, 0xe3, 0xab, 0xdd, 0x92, 0xc2, 0xee, 0x00, 0xa5, 0xa3, 0xa1, 0x2b, 0xd9,
			0x67, 0x99, 0x31, 0xad, 0x15, 0x87, 0x3b, 0xfa, 0xbb, 0x22, 0x6d, 0x43, 0x46, 0xca, 0xc7, 0x5c,
			0x1f, 0x23, 0xbd, 0x31, 0x2e, 0x01, 0xaf, 0xa1, 0xfe, 0x6b, 0x4e, 0x61, 0x19, 0xec, 0x14, 0xfd,
			0xaf, 0xf8, 0x63, 0xec, 0x71, 0xc2, 0x8c, 0xf2, 0xa6, 0xf1, 0x1d, 0x40, 0x5c, 0x13, 0x92, 0xb0,
			0x00, 0xa6, 0xe3, 0xd5, 0x75, 0x7f, 0x8f, 0xa5, 0xcc, 0x97, 0x8b, 0x80, 0xf3, 0xff, 0xe6, 0xcc,
			0x67, 0x7d, 0x87, 0xae, 0x5f, 0xe5, 0x77, 0xc0, 0xc8, 0xb9, 0xd0, 0xa2, 0x21, 0xe4, 0x22, 0x42,
			0xf9, 0xb5, 0x96, 0x9f, 0x96, 0xb7, 0x4d, 0xa8, 0xf1, 0x41, 0x20, 0xb6, 0x86, 0xd2, 0x3c, 0x32,
			0xcb, 0x70, 0x97, 0x20, 0x6e, 0x60, 0xe7, 0x4f, 0xc8, 0x10, 0x1f, 0x97, 0xbd, 0x30, 0x06, 0x1d,
			0x6b, 0x34, 0x9c, 0xb0, 0xe7, 0xe0, 0xf5, 0xcc, 0xe9, 0x98, 0xcd, 0xa1, 0x2a, 0x8a, 0xef, 0xe5,
			0xcd, 0x98, 0x2a, 0x52, 0xe9, 0x4d, 0xeb, 0x5c, 0x28, 0xc4, 0xd3, 0x46, 0x00, 0x25, 0x4e, 0x83,
			0xdd, 0xfb, 0xe2, 0xff, 0x31, 0x46, 0x7b, 0x93, 0xf2, 0xcd, 0xd9, 0x27, 0x35, 0x6c, 0x8e, 0x18,
			0x3b, 0xac, 0xe1, 0xa5, 0x52, 0x93, 0x3b, 0x1c, 0x9e, 0x13, 0x57, 0x30, 0x72, 0xce, 0x9e, 0xae,
			0x6e, 0x88, 0x30, 0xc3, 0x05, 0xbd, 0x1f, 0x64, 0xeb, 0x44, 0x30, 0xff, 0x9e, 0xd0, 0x84, 0x6b,
			0x50, 0x3b, 0x5b, 0x98, 0x9d, 0x54, 0x4c, 0x90, 0xd2, 0x72, 0x8e, 0xe4, 0x9e, 0xad, 0xd8, 0x9c,
			0x0f, 0x87, 0x14, 0x9f, 0x15, 0xcb, 0xe9, 0x3a, 0xcf, 0xa6, 0xb3, 0x54, 0x7f, 0xad, 0x9d, 0xbe,
			0x9c, 0x95, 0x12, 0x77, 0x62, 0xd9, 0x46, 0xeb, 0x56, 0xce, 0xdb, 0x25, 0x32, 0xa0, 0xd7, 0x6d,
			0x18, 0xcd, 0x6a, 0x01, 0xd8, 0x3f, 0x91, 0x93, 0x53, 0x70, 0x65, 0xde, 0x35, 0xf3, 0xd3, 0x5f,
			0x6b, 0x44, 0x42, 0x12, 0xb2, 0xca, 0x22, 0x3a, 0xf7, 0x4d, 0xdd, 0x76, 0x28, 0x2a, 0x1d, 0x4c,
			0x03, 0xc6, 0x3f, 0xcd, 0xdf, 0x84, 0x03, 0xbe, 0x78, 0x1b, 0x73, 0x17, 0xa6, 0xbc, 0x78, 0x2f,
			0x79, 0x02, 0x2c, 0x45, 0xc7, 0x6d, 0xcf, 0x00, 0xb8, 0xaf, 0x34, 0x5a, 0x18, 0xdc, 0xec, 0xd7,
			0xfb, 0x0a, 0xbe, 0x3d, 0x46, 0xf1, 0x47, 0xfb, 0x05, 0x80, 0x0c, 0xe1, 0x7a, 0x61, 0x2d, 0xd3,
			0x91, 0xcb, 0x1d, 0x8f, 0x1d, 0x9d, 0xd1, 0x32, 0xd2, 0x22, 0xb0, 0x17, 0xf9, 0xd9, 0xbc, 0x93,
			0xcd, 0x75, 0xad, 0x86, 0xb3, 0xf8, 0x1a, 0x9c, 0xde, 0xaa, 0x9d, 0x8c, 0x2a, 0xda, 0x2c, 0xb4,
			0x5c, 0x54, 0x71, 0x1a, 0x43, 0xdb, 0xd9, 0xfe, 0xb1, 0xb9, 0x05, 0x45, 0xca, 0xd6, 0x65, 0xfa,
			0xb2, 0x1e, 0x47, 0x77, 0x87, 0x47, 0x2e, 0x11, 0x5a, 0x79, 0xf9, 0x1f, 0xac, 0x8b, 0x83, 0x17,
			0x50, 0xbd, 0x9a, 0x99, 0x34, 0xb2, 0x10, 0x43, 0xb2, 0x5d, 0xd0, 0x7e, 0xf5, 0xb5, 0x44, 0xa5,
			0x67, 0xb0, 0x52, 0x2f, 0x4d, 0xad, 0x86, 0xe5, 0x3e, 0x9f, 0x0e, 0xac, 0x53, 0xbe, 0xb7, 0x47,
			0xb0, 0x32, 0x67, 0xec, 0xc3, 0x91, 0x0e, 0x0d, 0x72, 0x7e, 0x8d, 0xe4, 0x2c, 0x5c, 0xd4, 0xa4,
			0xaa, 0xf2, 0x9a, 0x59, 0x78, 0x88, 0x3a, 0x50, 0x6d, 0x02, 0x03, 0x01, 0x00, 0x01
		};

		public override string ManagedSubjectName {
			get {
				return managedSubject;
			}
		}

		public override string ManagedIssuerName {
			get {
				return managedIssuer;
			}
		}

		public override byte[] Hash {
			get {
				return hash;
			}
		}

		public override CertificateNameInfo IssuerName {
			get {
				return issuerName;
			}
		}

		public override string IssuerNameString {
			get {
				return issuer;
			}
		}

		public override DateTime NotAfter {
			get {
				return notAfter;
			}
		}

		public override DateTime NotBefore {
			get {
				return notBefore;
			}
		}

		public override string PublicKeyAlgorithmOid {
			get {
				return Oid_Rsa;
			}
		}

		public override byte[] PublicKeyData {
			get {
				return publicKeyData;
			}
		}

		public override byte[] PublicKeyParameters {
			get {
				return EmptyPublicKeyParameters;
			}
		}

		public override byte[] SerialNumber {
			get {
				return serial;
			}
		}

		public override byte[] SerialNumberMono {
			get {
				return serialMono;
			}
		}

		public override string SignatureAlgorithmOid {
			get {
				return Oid_RsaWithSha256;
			}
		}

		public override CertificateNameInfo SubjectName {
			get {
				return subjectName;
			}
		}

		public override string SubjectNameString {
			get {
				return subject;
			}
		}

		public override int Version {
			get {
				return 3;
			}
		}
	}
}

