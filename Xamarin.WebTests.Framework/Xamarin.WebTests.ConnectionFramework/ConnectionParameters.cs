﻿using System;
using System.Net.Security;
using System.Security.Cryptography.X509Certificates;
using Xamarin.AsyncTests;
using Xamarin.AsyncTests.Portable;

namespace Xamarin.WebTests.ConnectionFramework
{
	public class ConnectionParameters : ITestParameter, ICloneable
	{
		public string Identifier {
			get;
		}

		string ITestParameter.Value => Identifier;

		string ITestParameter.FriendlyValue => Identifier;

		public ConnectionParameters (string identifier, X509Certificate serverCertificate)
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
			ExpectClientException = other.ExpectClientException;
			ExpectServerException = other.ExpectServerException;
			SslStreamFlags = other.SslStreamFlags;
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

		public string TargetHost {
			get; set;
		}

		public X509Certificate ClientCertificate {
			get; set;
		}

		public CertificateValidator ClientCertificateValidator {
			get; set;
		}

		public CertificateSelector ClientCertificateSelector {
			get; set;
		}

		public X509Certificate ServerCertificate {
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

		public bool ExpectClientException {
			get; set;
		}

		public bool ExpectServerException {
			get; set;
		}

		public SslStreamFlags SslStreamFlags {
			get; set;
		}
	}
}

