#region Copyright
//=======================================================================================
// Microsoft Azure Customer Advisory Team 
//
// This sample is supplemental to the technical guidance published on my personal
// blog at http://blogs.msdn.com/b/paolos/. 
// 
// Author: Paolo Salvatori
//=======================================================================================
// Copyright (c) Microsoft Corporation. All rights reserved.
// 
// LICENSED UNDER THE APACHE LICENSE, VERSION 2.0 (THE "LICENSE"); YOU MAY NOT USE THESE 
// FILES EXCEPT IN COMPLIANCE WITH THE LICENSE. YOU MAY OBTAIN A COPY OF THE LICENSE AT 
// http://www.apache.org/licenses/LICENSE-2.0
// UNLESS REQUIRED BY APPLICABLE LAW OR AGREED TO IN WRITING, SOFTWARE DISTRIBUTED UNDER THE 
// LICENSE IS DISTRIBUTED ON AN "AS IS" BASIS, WITHOUT WARRANTIES OR CONDITIONS OF ANY 
// KIND, EITHER EXPRESS OR IMPLIED. SEE THE LICENSE FOR THE SPECIFIC LANGUAGE GOVERNING 
// PERMISSIONS AND LIMITATIONS UNDER THE LICENSE.
//=======================================================================================
#endregion

using System.Threading.Tasks;

using Azure.Core;
using Azure.Messaging.ServiceBus;
using Azure.Messaging.ServiceBus.Administration;

using ServiceBusExplorer.Utilities.Helpers;

// ReSharper disable CheckNamespace
namespace ServiceBusExplorer.ServiceBus.Helpers
// ReSharper restore CheckNamespace
{
    public class ServiceBusHelper2
    {
        readonly WriteToLogDelegate writeToLog;

        public string ConnectionString { get; set; }
        public ServiceBusTransportType TransportType { get; set; }

        /// <summary>
        /// When set, modern Azure.Messaging.ServiceBus clients are created with this credential
        /// (against <see cref="FullyQualifiedNamespace"/>) instead of from <see cref="ConnectionString"/>.
        /// Used for Entra ID-protected namespaces.
        /// </summary>
        public TokenCredential TokenCredential { get; set; }

        /// <summary>
        /// Fully-qualified namespace (e.g. "mybus.servicebus.windows.net") used when
        /// <see cref="TokenCredential"/> is set.
        /// </summary>
        public string FullyQualifiedNamespace { get; set; }

        public bool UsesEntraId => TokenCredential != null && !string.IsNullOrWhiteSpace(FullyQualifiedNamespace);

        public WriteToLogDelegate WriteToLog
        {
            get
            {
                return writeToLog;
            }
        }

        public ServiceBusHelper2(WriteToLogDelegate writeToLog)
        {
            this.writeToLog = writeToLog;
        }

        public bool ConnectionStringContainsEntityPath()
        {
            if (UsesEntraId || string.IsNullOrWhiteSpace(ConnectionString))
            {
                return false;
            }

            try
            {
                var connectionStringProperties = ServiceBusConnectionStringProperties.Parse(ConnectionString);
                return connectionStringProperties?.EntityPath != null;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        ///  Dispose of the returned ServiceBusClient object by calling DisposeAsync().
        /// </summary>
        /// <returns>An Azure.Messaging.ServiceBus.ServiceBusClient</returns>
        public ServiceBusClient CreateServiceBusClient()
        {
            var clientOptions = new ServiceBusClientOptions { TransportType = this.TransportType };

            if (UsesEntraId)
            {
                return new ServiceBusClient(FullyQualifiedNamespace, TokenCredential, clientOptions);
            }

            return new ServiceBusClient(ConnectionString, clientOptions);
        }

        public ServiceBusAdministrationClient CreateAdministrationClient()
        {
            if (UsesEntraId)
            {
                return new ServiceBusAdministrationClient(FullyQualifiedNamespace, TokenCredential);
            }

            return new ServiceBusAdministrationClient(ConnectionString);
        }

        public async Task<bool> IsPremiumNamespace()
        {
            var administrationClient = CreateAdministrationClient();
            NamespaceProperties namespaceProperties = await administrationClient.GetNamespacePropertiesAsync().ConfigureAwait(false);

            return namespaceProperties.MessagingSku == MessagingSku.Premium;
        }

        public async Task<bool> IsQueue(string name)
        {
            var administrationClient = CreateAdministrationClient();
            return await administrationClient.QueueExistsAsync(name).ConfigureAwait(false);
        }

        public async Task<bool> IsTopic(string name)
        {
            var administrationClient = CreateAdministrationClient();
            return await administrationClient.TopicExistsAsync(name).ConfigureAwait(false);
        }
    }
}
