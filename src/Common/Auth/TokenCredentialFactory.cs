#region Copyright
//=======================================================================================
// Copyright (c) Microsoft Corporation. All rights reserved.
//=======================================================================================
#endregion

using System;
using System.Security.Cryptography.X509Certificates;
using Azure.Core;
using Azure.Identity;

namespace ServiceBusExplorer.Auth
{
    public static class TokenCredentialFactory
    {
        public static TokenCredential Create(EntraIdAuthenticationOptions options)
        {
            if (options == null)
            {
                throw new ArgumentNullException(nameof(options));
            }

            switch (options.Mode)
            {
                case AuthenticationMode.ManagedIdentitySystemAssigned:
                    return new ManagedIdentityCredential();

                case AuthenticationMode.ManagedIdentityUserAssigned:
                    if (string.IsNullOrWhiteSpace(options.ClientId))
                    {
                        throw new ArgumentException(
                            "ClientId is required for user-assigned managed identity.",
                            nameof(options));
                    }
                    return new ManagedIdentityCredential(options.ClientId);

                case AuthenticationMode.DefaultAzureCredential:
                    var dacOptions = new DefaultAzureCredentialOptions();
                    if (!string.IsNullOrWhiteSpace(options.TenantId))
                    {
                        dacOptions.TenantId = options.TenantId;
                    }
                    if (!string.IsNullOrWhiteSpace(options.ClientId))
                    {
                        dacOptions.ManagedIdentityClientId = options.ClientId;
                    }
                    return new DefaultAzureCredential(dacOptions);

                case AuthenticationMode.ServicePrincipalSecret:
                    if (string.IsNullOrWhiteSpace(options.TenantId) ||
                        string.IsNullOrWhiteSpace(options.ClientId) ||
                        string.IsNullOrWhiteSpace(options.ClientSecret))
                    {
                        throw new ArgumentException(
                            "TenantId, ClientId and ClientSecret are required for service principal (secret) authentication.",
                            nameof(options));
                    }
                    return new ClientSecretCredential(options.TenantId, options.ClientId, options.ClientSecret);

                case AuthenticationMode.ServicePrincipalCertificate:
                    if (string.IsNullOrWhiteSpace(options.TenantId) ||
                        string.IsNullOrWhiteSpace(options.ClientId) ||
                        string.IsNullOrWhiteSpace(options.CertificateThumbprint))
                    {
                        throw new ArgumentException(
                            "TenantId, ClientId and CertificateThumbprint are required for service principal (certificate) authentication.",
                            nameof(options));
                    }
                    var certificate = LoadCertificate(options.CertificateThumbprint);
                    return new ClientCertificateCredential(options.TenantId, options.ClientId, certificate);

                case AuthenticationMode.InteractiveBrowser:
                    var ibcOptions = new InteractiveBrowserCredentialOptions();
                    if (!string.IsNullOrWhiteSpace(options.TenantId))
                    {
                        ibcOptions.TenantId = options.TenantId;
                    }
                    if (!string.IsNullOrWhiteSpace(options.ClientId))
                    {
                        ibcOptions.ClientId = options.ClientId;
                    }
                    return new InteractiveBrowserCredential(ibcOptions);

                case AuthenticationMode.Sas:
                    throw new InvalidOperationException(
                        "TokenCredentialFactory cannot create a credential for SAS-based authentication.");

                default:
                    throw new ArgumentOutOfRangeException(
                        nameof(options),
                        options.Mode,
                        "Unknown authentication mode.");
            }
        }

        private static X509Certificate2 LoadCertificate(string thumbprint)
        {
            var sanitized = (thumbprint ?? string.Empty)
                .Replace(" ", string.Empty)
                .Replace(":", string.Empty);

            foreach (var location in new[] { StoreLocation.CurrentUser, StoreLocation.LocalMachine })
            {
                using (var store = new X509Store(StoreName.My, location))
                {
                    store.Open(OpenFlags.ReadOnly);
                    var matches = store.Certificates.Find(X509FindType.FindByThumbprint, sanitized, validOnly: false);
                    if (matches.Count > 0)
                    {
                        return matches[0];
                    }
                }
            }

            throw new InvalidOperationException(
                $"Certificate with thumbprint '{thumbprint}' was not found in CurrentUser/My or LocalMachine/My.");
        }
    }
}
