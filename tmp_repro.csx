using System;
using ServiceBusExplorer.Auth;
using ServiceBusExplorer.Helpers;
using Microsoft.ServiceBus.Messaging;

class P {
  static void Main() {
    var opts = new EntraIdAuthenticationOptions { Mode = AuthenticationMode.ManagedIdentityUserAssigned, ClientId = "abc", TenantId = "def" };
    var cs = ServiceBusNamespace.BuildEntraIdConnectionString("mybus.servicebus.windows.net", opts, TransportType.Amqp);
    Console.WriteLine("CS=" + cs);
    var ns = ServiceBusNamespace.GetServiceBusNamespace("test", cs, (m,a) => Console.WriteLine("LOG: " + m));
    Console.WriteLine("ns=" + (ns == null ? "NULL" : ns.ConnectionStringType.ToString() + " fqdn=" + ns.FullyQualifiedNamespace));
  }
}
