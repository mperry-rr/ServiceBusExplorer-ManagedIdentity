#region Copyright
//=======================================================================================
// Copyright (c) Microsoft Corporation. All rights reserved.
//=======================================================================================
#endregion

using System;
using System.Threading;
using Azure.Core;
using Microsoft.ServiceBus;

namespace ServiceBusExplorer.Auth
{
    /// <summary>
    /// Adapts an <see cref="Azure.Core.TokenCredential"/> (from Azure.Identity) into a
    /// <see cref="Microsoft.ServiceBus.TokenProvider"/> understood by the legacy
    /// <c>WindowsAzure.ServiceBus</c> SDK so all AAD authentication modes (managed identity,
    /// DefaultAzureCredential, service principal, interactive browser) work in both SDK stacks.
    /// </summary>
    public static class LegacyAadTokenProviderFactory
    {
        // Service Bus is a single AAD resource regardless of the namespace endpoint.
        // See https://learn.microsoft.com/azure/service-bus-messaging/service-bus-managed-service-identity
        public const string ServiceBusAadResource = "https://servicebus.azure.net/";
        public const string EventHubsAadResource = "https://eventhubs.azure.net/";
        private const string ServiceBusAadScope = "https://servicebus.azure.net/.default";
        private const string EventHubsAadScope = "https://eventhubs.azure.net/.default";

        public static TokenProvider Create(EntraIdAuthenticationOptions options)
        {
            return Create(options, ServiceBusAadResource);
        }

        /// <summary>
        /// Creates a TokenProvider targeting the specified AAD resource (audience).
        /// Use <see cref="ServiceBusAadResource"/> for Service Bus namespaces and
        /// <see cref="EventHubsAadResource"/> for Event Hub namespaces.
        /// </summary>
        public static TokenProvider Create(EntraIdAuthenticationOptions options, string audience)
        {
            if (options == null)
            {
                throw new ArgumentNullException(nameof(options));
            }

            if (!options.IsEntraId)
            {
                throw new InvalidOperationException(
                    "LegacyAadTokenProviderFactory only supports Entra ID authentication modes.");
            }

            var resource = string.IsNullOrWhiteSpace(audience) ? ServiceBusAadResource : audience;
            var scope = ResourceToScope(resource);

            // Prefer the SDK's first-class MSI provider for system-assigned MI when possible.
            // It uses the IMDS endpoint without taking a dependency on Azure.Identity at runtime.
            if (options.Mode == AuthenticationMode.ManagedIdentitySystemAssigned &&
                string.IsNullOrWhiteSpace(options.ClientId))
            {
                return TokenProvider.CreateManagedIdentityTokenProvider(new Uri(resource));
            }

            var credential = TokenCredentialFactory.Create(options);
            var audienceUri = new Uri(resource);

            // The legacy SDK passes us the namespace URI as "audience"; we ignore it because
            // the AAD resource for the data plane is determined by the caller's audience selection.
            return TokenProvider.CreateAzureActiveDirectoryTokenProvider(
                async (audienceArg, authority, state) =>
                {
                    var token = await credential
                        .GetTokenAsync(new TokenRequestContext(new[] { scope }), CancellationToken.None)
                        .ConfigureAwait(false);
                    return token.Token;
                },
                audienceUri,
                "https://login.microsoftonline.com/" + (options.TenantId ?? "common"),
                null);
        }

        private static string ResourceToScope(string resource)
        {
            var trimmed = resource.TrimEnd('/');
            return trimmed + "/.default";
        }

        /// <summary>
        /// Token provider for the Notification Hubs SDK. Returns null when the installed Notification Hubs
        /// SDK version does not support AAD authentication; callers should skip notification-hub
        /// initialization in that case.
        /// </summary>
        public static Microsoft.Azure.NotificationHubs.TokenProvider CreateForNotificationHubs(EntraIdAuthenticationOptions options)
        {
            // The Microsoft.Azure.NotificationHubs 1.0.9 SDK shipped with this project predates first-class
            // AAD support. Returning null lets the caller fall back gracefully (notification hubs simply
            // won't be enumerated when connecting via Entra ID).
            _ = options;
            return null;
        }

        public static string ServiceBusResource => ServiceBusAadResource;
    }
}
