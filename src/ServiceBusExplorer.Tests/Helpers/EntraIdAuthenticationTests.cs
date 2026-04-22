using Azure.Identity;
using FluentAssertions;
using Microsoft.ServiceBus.Messaging;
using ServiceBusExplorer.Auth;
using ServiceBusExplorer.Helpers;
using Xunit;

namespace ServiceBusExplorer.Tests.Helpers
{
    public class EntraIdAuthenticationTests
    {
        [Theory]
        [InlineData("Managed Identity", AuthenticationMode.ManagedIdentitySystemAssigned)]
        [InlineData("ManagedIdentity", AuthenticationMode.ManagedIdentitySystemAssigned)]
        [InlineData("MSI", AuthenticationMode.ManagedIdentitySystemAssigned)]
        [InlineData("ManagedIdentityUserAssigned", AuthenticationMode.ManagedIdentityUserAssigned)]
        [InlineData("DefaultAzureCredential", AuthenticationMode.DefaultAzureCredential)]
        [InlineData("Default", AuthenticationMode.DefaultAzureCredential)]
        [InlineData("ServicePrincipal", AuthenticationMode.ServicePrincipalSecret)]
        [InlineData("ClientSecret", AuthenticationMode.ServicePrincipalSecret)]
        [InlineData("ServicePrincipalCertificate", AuthenticationMode.ServicePrincipalCertificate)]
        [InlineData("Certificate", AuthenticationMode.ServicePrincipalCertificate)]
        [InlineData("InteractiveBrowser", AuthenticationMode.InteractiveBrowser)]
        [InlineData("Browser", AuthenticationMode.InteractiveBrowser)]
        public void ParseAuthenticationMode_recognises_aliases(string text, AuthenticationMode expected)
        {
            ServiceBusNamespace.ParseAuthenticationMode(text).Should().Be(expected);
        }

        [Fact]
        public void BuildEntraIdConnectionString_includes_required_fields()
        {
            var options = new EntraIdAuthenticationOptions
            {
                Mode = AuthenticationMode.ManagedIdentityUserAssigned,
                ClientId = "11111111-1111-1111-1111-111111111111",
                TenantId = "22222222-2222-2222-2222-222222222222",
            };

            var cs = ServiceBusNamespace.BuildEntraIdConnectionString(
                "mybus.servicebus.windows.net", options, TransportType.Amqp);

            cs.Should().Contain("Endpoint=sb://mybus.servicebus.windows.net/");
            cs.Should().Contain("Authentication=");
            cs.Should().Contain("ClientId=11111111-1111-1111-1111-111111111111");
            cs.Should().Contain("TenantId=22222222-2222-2222-2222-222222222222");
            cs.Should().Contain("TransportType=Amqp");
        }

        [Fact]
        public void GetServiceBusNamespace_round_trips_AAD_connection_string()
        {
            var options = new EntraIdAuthenticationOptions
            {
                Mode = AuthenticationMode.ManagedIdentityUserAssigned,
                ClientId = "abc",
                TenantId = "def",
            };
            var cs = ServiceBusNamespace.BuildEntraIdConnectionString(
                "mybus.servicebus.windows.net", options, TransportType.Amqp);

            var ns = ServiceBusNamespace.GetServiceBusNamespace("test", cs, (m, a) => { });

            ns.Should().NotBeNull();
            ns.ConnectionStringType.Should().Be(ServiceBusNamespaceType.EntraId);
            ns.FullyQualifiedNamespace.Should().Be("mybus.servicebus.windows.net");
            ns.EntraIdAuthentication.Should().NotBeNull();
            ns.EntraIdAuthentication.Mode.Should().Be(AuthenticationMode.ManagedIdentityUserAssigned);
            ns.EntraIdAuthentication.ClientId.Should().Be("abc");
            ns.EntraIdAuthentication.TenantId.Should().Be("def");
        }

        [Fact]
        public void TokenCredentialFactory_returns_ManagedIdentity_for_system_assigned()
        {
            var cred = TokenCredentialFactory.Create(new EntraIdAuthenticationOptions
            {
                Mode = AuthenticationMode.ManagedIdentitySystemAssigned,
            });
            cred.Should().BeOfType<ManagedIdentityCredential>();
        }

        [Fact]
        public void TokenCredentialFactory_returns_ManagedIdentity_for_user_assigned()
        {
            var cred = TokenCredentialFactory.Create(new EntraIdAuthenticationOptions
            {
                Mode = AuthenticationMode.ManagedIdentityUserAssigned,
                ClientId = "11111111-1111-1111-1111-111111111111",
            });
            cred.Should().BeOfType<ManagedIdentityCredential>();
        }

        [Fact]
        public void TokenCredentialFactory_returns_DefaultAzureCredential()
        {
            var cred = TokenCredentialFactory.Create(new EntraIdAuthenticationOptions
            {
                Mode = AuthenticationMode.DefaultAzureCredential,
            });
            cred.Should().BeOfType<DefaultAzureCredential>();
        }

        [Fact]
        public void TokenCredentialFactory_returns_ClientSecret_for_service_principal()
        {
            var cred = TokenCredentialFactory.Create(new EntraIdAuthenticationOptions
            {
                Mode = AuthenticationMode.ServicePrincipalSecret,
                ClientId = "11111111-1111-1111-1111-111111111111",
                TenantId = "22222222-2222-2222-2222-222222222222",
                ClientSecret = "secret",
            });
            cred.Should().BeOfType<ClientSecretCredential>();
        }

        [Fact]
        public void TokenCredentialFactory_returns_InteractiveBrowser()
        {
            var cred = TokenCredentialFactory.Create(new EntraIdAuthenticationOptions
            {
                Mode = AuthenticationMode.InteractiveBrowser,
            });
            cred.Should().BeOfType<InteractiveBrowserCredential>();
        }
    }
}
