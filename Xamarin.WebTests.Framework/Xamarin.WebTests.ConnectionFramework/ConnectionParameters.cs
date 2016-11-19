﻿using System;
using System.Net.Security;
using System.Security.Cryptography.X509Certificates;
using Xamarin.AsyncTests;
using Xamarin.AsyncTests.Portable;

namespace Xamarin.WebTests.ConnectionFramework
{
	using Resources;

	public class ConnectionParameters : ITestParameter, ICloneable
	{
		public string Identifier {
			get;
			private set;
		}

		string ITestParameter.Value {
			get { return Identifier; }
		}

		public ConnectionParameters (string identifier, CertificateResourceType serverCertificate)
		{
			Identifier = identifier;
			ServerCertificate = serverCertificate;
		}

		protected ConnectionParameters (ConnectionParameters other)
		{
			Identifier = other.Identifier;
			EndPoint = other.EndPoint;
			ListenAddress = other.ListenAddress;
			ProtocolVersion = other.ProtocolVersion;
			UseStreamInstrumentation = other.UseStreamInstrumentation;
			TargetHost = other.TargetHost;
			ClientCertificate = other.ClientCertificate;
			ClientCertificateValidator = other.ClientCertificateValidator;
			ClientCertificateSelector = other.ClientCertificateSelector;
			ServerCertificate = other.ServerCertificate;
			ServerCertificateValidator = other.ServerCertificateValidator;
			AskForClientCertificate = other.AskForClientCertificate;
			RequireClientCertificate = other.RequireClientCertificate;
			EnableDebugging = other.EnableDebugging;
			ExpectPolicyErrors = other.ExpectPolicyErrors;
			ExpectChainStatus = other.ExpectChainStatus;
			GlobalValidationFlags = other.GlobalValidationFlags;
			if (other.ValidationParameters != null)
				ValidationParameters = other.ValidationParameters.DeepClone ();
		}

		object ICloneable.Clone ()
		{
			return DeepClone ();
		}

		public virtual ConnectionParameters DeepClone ()
		{
			return new ConnectionParameters (this);
		}

		public IPortableEndPoint EndPoint {
			get; set;
		}

		public IPortableEndPoint ListenAddress {
			get; set;
		}

		public ProtocolVersions? ProtocolVersion {
			get; set;
		}

		public bool UseStreamInstrumentation {
			get; set;
		}

		public string TargetHost {
			get; set;
		}

		public CertificateResourceType? ClientCertificate {
			get; set;
		}

		public CertificateValidator ClientCertificateValidator {
			get; set;
		}

		public CertificateSelector ClientCertificateSelector {
			get; set;
		}

		public CertificateResourceType ServerCertificate {
			get; set;
		}

		public CertificateValidator ServerCertificateValidator {
			get; set;
		}

		public bool AskForClientCertificate {
			get; set;
		}

		public bool RequireClientCertificate {
			get; set;
		}

		public bool EnableDebugging {
			get;
			set;
		}

		public GlobalValidationFlags GlobalValidationFlags {
			get; set;
		}

		public SslPolicyErrors? ExpectPolicyErrors {
			get; set;
		}

		public X509ChainStatusFlags? ExpectChainStatus {
			get; set;
		}

		public ValidationParameters ValidationParameters {
			get; set;
		}
	}
}

