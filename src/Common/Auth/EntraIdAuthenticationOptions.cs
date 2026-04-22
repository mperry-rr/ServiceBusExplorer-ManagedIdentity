#region Copyright
//=======================================================================================
// Copyright (c) Microsoft Corporation. All rights reserved.
//=======================================================================================
#endregion

namespace ServiceBusExplorer.Auth
{
    public class EntraIdAuthenticationOptions
    {
        public AuthenticationMode Mode { get; set; } = AuthenticationMode.Sas;

        public string ClientId { get; set; }

        public string TenantId { get; set; }

        public string ClientSecret { get; set; }

        public string CertificateThumbprint { get; set; }

        public bool IsEntraId => Mode != AuthenticationMode.Sas;
    }
}
