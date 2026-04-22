**Author:** Paolo Salvatori ([@babosbird](https://twitter.com/babosbird))  
**Collaborators:**  Sean Feldman ([@sfeldman](https://twitter.com/sfeldman)) and Erik Mogensen ([@koltrast](https://twitter.com/koltrast))  
**Contributors:** [Many](https://github.com/paolosalvatori/ServiceBusExplorer/graphs/contributors)

# Service Bus Explorer
The Service Bus Explorer allows users to efficiently administer messaging entities. The tool provides advanced features like import/export functionality and the ability to test topic, queues, subscriptions, relay services, notification hubs and events hubs.

## Features

- **Microsoft Entra ID / Managed Identity authentication** — connect to AAD-protected Service Bus namespaces without shared-access keys. See [Connecting with Microsoft Entra ID](#connecting-with-microsoft-entra-id-managed-identity) below.
- **Dashboard tab** — live overview of message counts (Active, Dead Letter, Scheduled, Total) for all queues and subscriptions, with auto-refresh and color-coded dead-letter alerts
- **TreeView search/filter** — real-time filtering of queues, topics and subscriptions; press Ctrl+F to focus
- **Copy message body** — one-click clipboard copy from the message preview pane
- Import/export of namespace configuration
- Send, receive and peek messages for queues, topics and subscriptions
- Dead-letter message management
- Relay services, Notification Hubs and Event Hubs support

![Service Bus Explorer](./media/service-bus-explorer.png)

# Download

Pre-built binaries for this fork are in [`dist/`](./dist). The easiest way to run it:

- Download [`dist/ServiceBusExplorer-ManagedIdentity.zip`](./dist/ServiceBusExplorer-ManagedIdentity.zip)
- Extract anywhere and run `ServiceBusExplorer.exe`

Alternatively copy the expanded folder [`dist/ServiceBusExplorer-ManagedIdentity/`](./dist/ServiceBusExplorer-ManagedIdentity) directly.

# Connecting with Microsoft Entra ID (Managed Identity)

This fork adds first-class support for Microsoft Entra ID / Managed Identity so you can connect to Service Bus namespaces that have SAS keys disabled (`disableLocalAuth: true`) or when you simply prefer identity-based auth.

## Supported authentication modes

| Mode | When to use |
|---|---|
| **DefaultAzureCredential** | Developer laptops. Uses your `az login` token, Visual Studio credentials, or environment variables — whichever is available. **Recommended for local development.** |
| **Managed Identity (System Assigned)** | Only when the tool runs on Azure compute (VM, App Service, Container Apps, AKS, Functions) that has a system-assigned identity. Relies on IMDS, so it won't work on a dev laptop. |
| **Managed Identity (User Assigned)** | Same as above but with a specific user-assigned managed identity; supply its Client ID. |
| **Service Principal (client secret)** | App registration + secret. Supply Client ID, Tenant ID, Client Secret. |
| **Service Principal (certificate)** | App registration + X.509 cert installed in `CurrentUser\My` or `LocalMachine\My`. Supply Client ID, Tenant ID, and the cert Thumbprint. |
| **Interactive browser sign-in** | Pops a browser for MFA-protected sign-in. Useful when `az login` isn't available. |

All modes use the AMQP transport — Service Bus + AAD requires AMQP; the transport combo is locked accordingly.

## Required Azure RBAC

Assign your identity one of the built-in Service Bus data roles on the namespace (or a specific queue/topic):

| Role | What it grants |
|---|---|
| **Azure Service Bus Data Receiver** | Receive messages, peek, complete, abandon, dead-letter |
| **Azure Service Bus Data Sender** | Send messages, schedule, cancel scheduled |
| **Azure Service Bus Data Owner** | Full data-plane access plus entity management (create/update/delete queues, topics, subscriptions, rules) |

For browsing **and** administering a namespace (creating queues etc.) you want Data Owner.

## Using the UI

1. Launch `ServiceBusExplorer.exe`.
2. **File → Connect using Entra ID (Service Bus)**.
3. In the dialog:
   - Pick **Authentication mode** (defaults to DefaultAzureCredential).
   - Fill in **Fully qualified namespace**, e.g. `mybus.servicebus.windows.net`.
   - Supply Client ID / Tenant ID / Secret / Thumbprint only if the selected mode requires them — irrelevant fields are hidden.
4. Click **OK**. The dialog builds an Entra-ID pseudo connection string and injects it into the main Connect form.
5. Click **Save** to add it to your namespace list (give it a memorable key), then **OK** to connect.

After saving, the connection appears in **File → Saved connections** just like any SAS connection.

## Using the command line

The executable accepts the same CLI flags as before, plus new Entra ID options:

```powershell
# DefaultAzureCredential (recommended for dev boxes)
ServiceBusExplorer.exe --fqdn mybus.servicebus.windows.net --auth DefaultAzureCredential

# Service principal (client secret)
ServiceBusExplorer.exe --fqdn mybus.servicebus.windows.net `
                       --auth ServicePrincipal `
                       --client-id <appId> `
                       --tenant-id <tenantId> `
                       --client-secret <secret>

# User-assigned managed identity (when running on Azure compute)
ServiceBusExplorer.exe --fqdn mybus.servicebus.windows.net `
                       --auth ManagedIdentityUserAssigned `
                       --client-id <uamiClientId>

# Interactive browser sign-in
ServiceBusExplorer.exe --fqdn mybus.servicebus.windows.net --auth InteractiveBrowser
```

`--auth` aliases accepted: `ManagedIdentity`, `MSI`, `ManagedIdentityUserAssigned`, `DefaultAzureCredential`, `Default`, `ServicePrincipal`, `ClientSecret`, `ServicePrincipalCertificate`, `Certificate`, `InteractiveBrowser`, `Browser`.

You can also pass a raw connection string directly:

```
ServiceBusExplorer.exe -c "Endpoint=sb://mybus.servicebus.windows.net/;Authentication=Managed Identity;TransportType=Amqp"
```

## Pseudo connection-string format

Saved Entra-ID connections are stored in the same settings file as SAS connections using this format:

```
Endpoint=sb://<fqdn>/;Authentication=<Mode>;ClientId=<guid>;TenantId=<guid>;TransportType=Amqp
```

`ClientId` / `TenantId` / `ClientSecret` / `CertificateThumbprint` are only included when the selected mode needs them.

## Troubleshooting

- **"Managed Identity not available"** on a laptop → use **DefaultAzureCredential** instead; MI only works on Azure compute.
- **"Unauthorized"** / 401 from Service Bus → you're authenticated but lack the Service Bus Data role. Ask an owner to assign `Azure Service Bus Data Receiver` (or Sender / Owner) on the namespace.
- **Nothing happens / "Messaging factory created" then silence** — ensure `az login` has a fresh token for the correct tenant: `az login --tenant <tenantId>`.
- **Notification Hubs are disabled** when connecting via Entra ID — the legacy Notification Hubs SDK does not support AAD. SAS connections still get Notification Hub support.

# Software requirements
The following software is required to run ServiceBusExplorer. It may run on other versions.

- Windows 10 or later
- .NET Framework 4.6.2

# Installation

It is strongly recommended to set `Configuration File for Settings and Connection Strings` to `User Configuration File` as shown in the figure below to reduce problems when upgrading. 
![UserConfiguration](./media/UserConfigFile.png)

> **_Note:_** The `ServiceBusExplorer.exe.config` in the application directory will get overwritten during the upgrade.
>
> If you have made changes to it, you should back it up before upgrading. If you follow the recommendation above then only advanced changes such as WCF configuration modifications cause this. 
>
> Do not overwite the new configuration file with the old file since the `runtime` section in the new must file not be modified. 

## Using [Chocolatey](https://chocolatey.org/install)

### Installing for the first time

```
choco install ServiceBusExplorer
```

### Upgrading

```
choco upgrade ServiceBusExplorer
```

The default location of the executable is `C:\ProgramData\chocolatey\lib\ServiceBusExplorer\tools\ServiceBusExplorer.exe`.

More information on our [Chocolatey page](https://chocolatey.org/packages/ServiceBusExplorer).

## Using [Scoop](https://scoop.sh)

> **__Warning_** The `scoop` package is not maintained by the ServiceBusExplorer project so carefully check the package and the URLs it uses before using it. Also, the current package keeps the old version of `ServiceBusExplorer.exe.config`. That may cause assembly loading issues so do not use it for upgrading.

```
scoop install extras/servicebusexplorer
```

The default location of the executable is `%USERPROFILE%\scoop\apps\servicebusexplorer\current\tools\ServiceBusExplorer.exe`.

## Using GitHub
```
curl -s https://api.github.com/repos/paolosalvatori/ServiceBusExplorer/releases/latest | grep browser_download_url | cut -d '"' -f 4
```

## Using [Winget](https://learn.microsoft.com/en-us/windows/package-manager/winget/)

````
winget install --id paolosalvatori.ServiceBusExplorer --source winget
````

# Contributions
There are no dedicated developers so development is entirely based on voluntary effort.

Here are some guidelines concerning contributions:

- All contributions should be done on `main`.
- Every pull request is built by GitHub Actions and should preferably be linked to a GitHub issue.
- Write unit tests, if applicable.
- We have started to migrate from the old SDK to the latest SDKs for Service Bus, Event Hubs, Relay and Notification Hubs. Therefore, new classes should not depend on the old SDK unless absolutely necessary.  


## Development Environment

Visual Studio 2022 17.8.0 or later is required to build the solution. 

When editing UI elements Visual Studio should run as a DPI-unaware process. For more information about this, see the [Visual Studio documentation](https://docs.microsoft.com/en-us/dotnet/framework/winforms/disable-dpi-awareness-visual-studio). In Visual Studio 2022 the informational bar looks like this ![AutoscalingTurnedOff](./media/AutoscalingTurnedOff.png) when it is running as a DPI-unaware process.


# Azure Service Bus
Microsoft Azure Service Bus is a reliable information delivery service. The purpose of this service is to make communication easier. When two or more parties want to exchange information, they need a communication facilitator. Service Bus is a brokered, or third-party communication mechanism. This is similar to a postal service in the physical world. Postal services make it very easy to send different kinds of letters and packages with a variety of delivery guarantees, anywhere in the world.

Similar to the postal service delivering letters, Service Bus is flexible information delivery from both the sender and the recipient. The messaging service ensures that the information is delivered even if the two parties are never both online at the same time, or if they aren't available at the exact same time. In this way, messaging is similar to sending a letter, while non-brokered communication is similar to placing a phone call (or how a phone call used to be - before call waiting and caller ID, which are much more like brokered messaging).

The message sender can also require a variety of delivery characteristics including transactions, duplicate detection, time-based expiration, and batching. These patterns have postal analogies as well: repeat delivery, required signature, address change, or recall.

For more information, feel free to read the official documentation [here](https://docs.microsoft.com/en-us/azure/service-bus-messaging/service-bus-messaging-overview).

# Service Bus Explorer for Windows Server
The Service Bus Explorer 2.1.0 can be used with the Service Bus for Windows Server 1.1. The Service Bus Explorer 2.1.0 uses a version of the [Microsoft.ServiceBus.dll](http://www.nuget.org/packages/WindowsAzure.ServiceBus/) client library which is compatible with the Service Bus for Windows Server 1.1 RTM version. You can download the source code of the Service Bus Explorer 2.1.0 [here](https://github.com/paolosalvatori/ServiceBusExplorer/releases/tag/2.1.0).

# Documentation
[Here](./docs/documentation.md) you can find the tool documentation and a log of the features implemented over time.

# Alternative Service Bus Management Tools
Service Bus Explorer is only one of the management tools available for Azure Service Bus.

Here are a couple of alternatives. We do not take responsibility for them though:

| Tool                                     | Description                                   |
| ---------------------------------------- | --------------------------------------------- |
| Microsoft Azure Management Portal        | SaaS, web based, extremely basic              |
| Serverless360                            | paid with free trial, SaaS, web based         |
| [PowerShell]                             | free, open source, cross platform             |
| [Purple Explorer] (active fork)          | free, open source, cross platform             |
| [Superbus]                               | paid with a free trial, macOS                 |
| [Service Bus Cloud Explorer]             | paid with a free basic plan, SaaS, web based  |
| [Service Bus TUI](https://github.com/MonsieurTib/service-bus-tui) | free, open source, cross platform, terminal based             |

[PowerShell]: https://docs.microsoft.com/en-us/azure/service-bus-messaging/service-bus-manage-with-ps
[Purple Explorer]: https://github.com/philipmat/PurpleExplorer
[Superbus]: https://superbus.app/
[Service Bus Cloud Explorer]: https://cloudbricks.io/products/service_bus_cloud_explorer/
