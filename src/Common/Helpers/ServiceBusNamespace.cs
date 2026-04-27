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

#region Using Directives

using System;
using Microsoft.ServiceBus;
using Microsoft.ServiceBus.Messaging;
using System.Text.RegularExpressions;

using System.Globalization;
using System.Linq;
using System.Collections.Generic;
using ServiceBusExplorer.Auth;
using ServiceBusExplorer.Utilities.Helpers;

#endregion

namespace ServiceBusExplorer.Helpers
{
    public enum ServiceBusNamespaceType
    {
        Custom,
        Cloud,
        OnPremises,
        EntraId
    }

    /// <summary>
    /// Identifies the authentication mechanism used to connect to a Service Bus namespace.
    /// </summary>
    public enum ServiceBusAuthMode
    {
        /// <summary>Shared Access Signature (connection-string based)</summary>
        Sas,
        /// <summary>On-premises Windows credentials</summary>
        Windows,
        /// <summary>Azure Active Directory / Microsoft Entra ID interactive browser sign-in</summary>
        AzureActiveDirectory
    }

    /// <summary>
    /// This class represents a service bus namespace address and authentication credentials
    /// </summary>
    public class ServiceBusNamespace
    {
        #region Private Constants
        //***************************
        // Constants for accessing configuration files
        //***************************
        const string ServiceBusNamespaces = "serviceBusNamespaces";

        //***************************
        // Messages
        //***************************
        const string ServiceBusNamespacesNotConfigured = "Service bus accounts have not been properly configured in the configuration file.";
        const string ServiceBusNamespaceIsNullOrEmpty = "The connection string for service bus entry {0} is null or empty.";
        const string ServiceBusNamespaceIsWrong = "The connection string for service bus namespace {0} is in the wrong format.";
        const string ServiceBusNamespaceEndpointIsNullOrEmpty = "The endpoint for the service bus namespace {0} is null or empty.";
        const string ServiceBusNamespaceEndpointPrefixedWithSb = "The endpoint for the service bus namespace {0} is being automatically prefixed with \"sb://\".";
        const string ServiceBusNamespaceStsEndpointIsNullOrEmpty = "The sts endpoint for the service bus namespace {0} is null or empty.";
        const string ServiceBusNamespaceRuntimePortIsNullOrEmpty = "The runtime port for the service bus namespace {0} is null or empty.";
        const string ServiceBusNamespaceManagementPortIsNullOrEmpty = "The management port for the service bus namespace {0} is null or empty.";
        const string ServiceBusNamespaceEndpointUriIsInvalid = "The endpoint URI for the service bus namespace {0} is invalid.";
        const string ServiceBusNamespaceSharedAccessKeyNameIsInvalid = "The SharedAccessKeyName for the service bus namespace {0} is invalid.";
        const string ServiceBusNamespaceSharedAccessKeyIsInvalid = "The SharedAccessKey for the service bus namespace {0} is invalid.";

        //***************************
        // Parameters
        //***************************
        const string ConnectionStringEndpoint = "endpoint";
        const string ConnectionStringSharedAccessKeyName = "sharedaccesskeyname";
        const string ConnectionStringSharedAccessKey = "sharedaccesskey";
        const string ConnectionStringStsEndpoint = "stsendpoint";
        const string ConnectionStringRuntimePort = "runtimeport";
        const string ConnectionStringManagementPort = "managementport";
        const string ConnectionStringWindowsUsername = "windowsusername";
        const string ConnectionStringWindowsDomain = "windowsdomain";
        const string ConnectionStringWindowsPassword = "windowspassword";
        const string ConnectionStringTransportType = "transporttype";
        const string ConnectionStringEntityPath = "entitypath";
        const string ConnectionStringAuthentication = "authentication";
        const string ConnectionStringClientId = "clientid";
        const string ConnectionStringTenantId = "tenantid";
        const string ConnectionStringClientSecret = "clientsecret";
        const string ConnectionStringCertificateThumbprint = "certificatethumbprint";

        #endregion

        #region Public Constants
        //***************************
        // Formats
        //***************************
        public const string SasConnectionStringFormat = "Endpoint={0};SharedAccessKeyName={1};SharedAccessKey={2};TransportType={3}";
        public const string SasConnectionStringEntityPathFormat = "Endpoint={0};SharedAccessKeyName={1};SharedAccessKey={2};TransportType={3};EntityPath={4}";
        public const string EntraIdConnectionStringFormat = "Endpoint={0};Authentication={1};TransportType={2}";
        #endregion

        #region Public Constructors
        /// <summary>
        /// Initializes a new instance of the ServiceBusHelper class.
        /// </summary>
        public ServiceBusNamespace()
        {
            ConnectionStringType = ServiceBusNamespaceType.Cloud;
            AuthMode = ServiceBusAuthMode.Sas;
            ConnectionString = default(string);
            Uri = default(string);
            Namespace = default(string);
            ServicePath = default(string);
            StsEndpoint = default(string);
            RuntimePort = default(string);
            ManagementPort = default(string);
            WindowsDomain = default(string);
            WindowsUserName = default(string);
            WindowsPassword = default(string);
            EntraIdAuthentication = new EntraIdAuthenticationOptions();
        }

        /// <summary>
        /// Initializes a ServiceBusNamespace that authenticates via Microsoft Entra ID
        /// (managed identity, DefaultAzureCredential, service principal, etc.).
        /// </summary>
        public ServiceBusNamespace(
            string fullyQualifiedNamespace,
            EntraIdAuthenticationOptions authentication,
            TransportType transportType = TransportType.Amqp,
            string entityPath = "",
            bool isUserCreated = false)
        {
            if (string.IsNullOrWhiteSpace(fullyQualifiedNamespace))
            {
                throw new ArgumentException("Fully qualified namespace must be provided.", nameof(fullyQualifiedNamespace));
            }

            if (authentication == null || !authentication.IsEntraId)
            {
                throw new ArgumentException("An Entra ID authentication mode must be specified.", nameof(authentication));
            }

            ConnectionStringType = ServiceBusNamespaceType.EntraId;
            EntraIdAuthentication = authentication;
            FullyQualifiedNamespace = NormalizeFqdn(fullyQualifiedNamespace);
            Namespace = FullyQualifiedNamespace.Split('.')[0];
            Uri = $"sb://{FullyQualifiedNamespace}/";
            TransportType = transportType;
            EntityPath = entityPath ?? string.Empty;
            UserCreated = isUserCreated;
            ConnectionString = BuildEntraIdConnectionString(FullyQualifiedNamespace, authentication, transportType, EntityPath);
        }

        /// <summary>
        /// Initializes a new instance of the ServiceBusNamespace class.
        /// </summary>
        /// <param name="connectionStringType">The service bus namespace connection string type.</param>
        /// <param name="connectionString">The service bus namespace connection string.</param>
        /// <param name="uri">The full address of the service bus namespace.</param>
        /// <param name="ns">The service bus namespace.</param>
        /// <param name="name">The issuer name of the shared secret credentials.</param>
        /// <param name="key">The issuer secret of the shared secret credentials.</param>
        /// <param name="servicePath">The service path that follows the host name section of the URI.</param>
        /// <param name="stsEndpoint">The sts endpoint of the service bus namespace.</param>
        /// <param name="transportType">The transport type to use to access the namespace.</param>
        /// <param name="isSas">True is is SAS connection string, false otherwise.</param>
        /// <param name="entityPath">Entity path connection string scoped to. Otherwise a default.</param>
        public ServiceBusNamespace(ServiceBusNamespaceType connectionStringType,
                                   string connectionString,
                                   string uri,
                                   string ns,
                                   string servicePath,
                                   string name,
                                   string key,
                                   string stsEndpoint,
                                   TransportType transportType,
                                   bool isSas = false,
                                   string entityPath = "",
                                   bool isUserCreated = false)
        {
            ConnectionStringType = connectionStringType;
            AuthMode = ServiceBusAuthMode.Sas;
            Uri = string.IsNullOrWhiteSpace(uri) ?
                  ServiceBusEnvironment.CreateServiceUri("sb", ns, servicePath).ToString() :
                  uri;

            ConnectionString = connectionString;
            Namespace = ns;

            if (isSas)
            {
                SharedAccessKeyName = name;
                SharedAccessKey = key;
            }
            else
            {
                ServicePath = servicePath;
            }

            TransportType = transportType;
            StsEndpoint = stsEndpoint;
            RuntimePort = default(string);
            ManagementPort = default(string);
            WindowsDomain = default(string);
            WindowsUserName = default(string);
            WindowsPassword = default(string);
            EntityPath = entityPath;
            UserCreated = isUserCreated;
        }

        /// <summary>
        /// Initializes a new instance of the ServiceBusNamespace class for an on-premises namespace.
        /// </summary>
        /// <param name="connectionString">The service bus namespace connection string.</param>
        /// <param name="endpoint">The endpoint of the service bus namespace.</param>
        /// <param name="stsEndpoint">The sts endpoint of the service bus namespace.</param>
        /// <param name="runtimePort">The runtime port.</param>
        /// <param name="managementPort">The management port.</param>
        /// <param name="windowsDomain">The Windows domain or machine name.</param>
        /// <param name="windowsUsername">The Windows user name.</param>
        /// <param name="windowsPassword">The Windows user password.</param>
        /// <param name="ns">The service bus namespace.</param>
        /// <param name="transportType">The transport type to use to access the namespace.</param>
        public ServiceBusNamespace(string connectionString,
                                   string endpoint,
                                   string stsEndpoint,
                                   string runtimePort,
                                   string managementPort,
                                   string windowsDomain,
                                   string windowsUsername,
                                   string windowsPassword,
                                   string ns,
                                   TransportType transportType,
                                   bool isUserCreated = false)
        {
            ConnectionStringType = ServiceBusNamespaceType.OnPremises;
            AuthMode = ServiceBusAuthMode.Windows;
            ConnectionString = connectionString;
            Uri = endpoint;
            var uri = new Uri(endpoint);


            if (string.IsNullOrWhiteSpace(endpoint))
            {
                Uri = ServiceBusEnvironment.CreateServiceUri(uri.Scheme, ns, null).ToString();
            }

            Namespace = ns;
            var settings = new MessagingFactorySettings();
            TransportType = settings.TransportType;
            StsEndpoint = stsEndpoint;
            RuntimePort = runtimePort;
            ManagementPort = managementPort;
            WindowsDomain = windowsDomain;
            WindowsUserName = windowsUsername;
            WindowsPassword = windowsPassword;
            TransportType = transportType;
            UserCreated = isUserCreated;
        }

        /// <summary>
        /// Initializes a new instance of the ServiceBusNamespace class for Azure Active Directory authentication.
        /// </summary>
        public ServiceBusNamespace(string endpoint,
                                   string ns,
                                   string tenantId,
                                   TransportType transportType,
                                   string entityPath = "",
                                   bool isUserCreated = false)
        {
            ConnectionStringType = ServiceBusNamespaceType.Cloud;
            AuthMode = ServiceBusAuthMode.AzureActiveDirectory;
            Uri = endpoint;
            Namespace = ns;
            TenantId = tenantId;
            TransportType = transportType;
            EntityPath = entityPath;
            UserCreated = isUserCreated;

            // No SAS keys or connection string for AAD
            ConnectionString = null;
            SharedAccessKeyName = null;
            SharedAccessKey = null;
            StsEndpoint = null;
            RuntimePort = null;
            ManagementPort = null;
            WindowsDomain = null;
            WindowsUserName = null;
            WindowsPassword = null;
        }
        #endregion

        #region Public methods
        public static ServiceBusNamespace GetServiceBusNamespace(string key, string connectionString,
            WriteToLogDelegate staticWriteToLog)
        {

            if (string.IsNullOrWhiteSpace(connectionString))
            {
                staticWriteToLog(string.Format(CultureInfo.CurrentCulture, ServiceBusNamespaceIsNullOrEmpty, key));
                return null;
            }

            var isUserCreated = !(key == "CustomConnectionString" || key == "SASConnectionString");
            var toLower = connectionString.ToLower();
            var parameters = connectionString.Split([';'], StringSplitOptions.RemoveEmptyEntries)
                .ToDictionary(s => s.Substring(0, s.IndexOf('=')).ToLower(), s => s.Substring(s.IndexOf('=') + 1));

            if (toLower.Contains(ConnectionStringEndpoint) &&
                toLower.Contains(ConnectionStringSharedAccessKeyName) &&
                toLower.Contains(ConnectionStringSharedAccessKey))
            {
                return GetServiceBusNamespaceUsingSAS(key, connectionString, staticWriteToLog, 
                    isUserCreated, parameters);
            }

            if (toLower.Contains(ConnectionStringEndpoint) &&
                toLower.Contains(ConnectionStringAuthentication))
            {
                return GetServiceBusNamespaceUsingEntraId(key, connectionString, staticWriteToLog,
                    isUserCreated, parameters);
            }

            if (toLower.Contains(ConnectionStringRuntimePort) ||
                toLower.Contains(ConnectionStringManagementPort) ||
                toLower.Contains(ConnectionStringWindowsUsername) ||
                toLower.Contains(ConnectionStringWindowsDomain) ||
                toLower.Contains(ConnectionStringWindowsPassword))
            {
                return GetServiceBusNamespaceUsingWindows(key, connectionString, staticWriteToLog, 
                    isUserCreated, toLower, parameters);
            }

            return null;
        }

        public static Dictionary<string, ServiceBusNamespace> GetMessagingNamespaces
            (TwoFilesConfiguration configuration, WriteToLogDelegate writeToLog)
        {
            var hashtable = configuration.GetHashtableFromSection(ServiceBusNamespaces);

            if (hashtable == null || hashtable.Count == 0)
            {
                writeToLog(ServiceBusNamespacesNotConfigured);
            }

            var serviceBusNamespaces = new Dictionary<string, ServiceBusNamespace>();

            if (hashtable == null)
            {
                return serviceBusNamespaces;
            }

            var e = hashtable.GetEnumerator();

            while (e.MoveNext())
            {
                if (!(e.Key is string) || !(e.Value is string))
                {
                    continue;
                }

                var serviceBusNamespace = ServiceBusNamespace.GetServiceBusNamespace((string)e.Key, (string)e.Value, writeToLog);

                if (serviceBusNamespace != null)
                {
                    serviceBusNamespaces.Add((string)e.Key, serviceBusNamespace);
                }
            }

            var microsoftServiceBusConnectionString =
                configuration.GetStringValue(ConfigurationParameters.MicrosoftServiceBusConnectionString);

            if (!string.IsNullOrWhiteSpace(microsoftServiceBusConnectionString))
            {
                var serviceBusNamespace = ServiceBusNamespace.GetServiceBusNamespace(ConfigurationParameters.MicrosoftServiceBusConnectionString, microsoftServiceBusConnectionString, writeToLog);

                if (serviceBusNamespace != null)
                {
                    serviceBusNamespaces.
                        Add(ConfigurationParameters.MicrosoftServiceBusConnectionString, serviceBusNamespace);
                }
            }

            return serviceBusNamespaces;
        }

        public static void SaveConnectionString(TwoFilesConfiguration configuration,
            string key, string value, WriteToLogDelegate staticWriteToLog)
        {
            configuration.AddEntryToDictionarySection(ServiceBusNamespaces, key, value);
        }
        #endregion

        #region Public Properties

        /// <summary>
        /// Gets or sets the service bus namespace type.
        /// </summary>
        public ServiceBusNamespaceType ConnectionStringType { get; set; }

        /// <summary>
        /// Get or set if this is a connection string added by the user
        /// </summary>
        public bool UserCreated { get; set; }

        /// <summary>
        /// Gets or sets the service bus namespace connection string.
        /// </summary>
        public string ConnectionString { get; set; }

        /// <summary>
        /// Gets or sets the service bus namespace connection string without transport type.
        /// </summary>
        public string ConnectionStringWithoutTransportType
        {
            get
            {
                if (ConnectionString == null)
                    return null;
                var regex = new Regex(@";TransportType=\w*;?");
                var connectionString = regex.Replace(ConnectionString, string.Empty);
                return connectionString;
            }
        }

        /// <summary>
        /// Gets or sets the full address of the service bus namespace.
        /// </summary>
        public string Uri { get; set; }

        /// <summary>
        /// Gets or sets the service bus namespace.
        /// </summary>
        public string Namespace { get; set; }

        /// <summary>
        /// Gets or sets the service path that follows the host name section of the URI.
        /// </summary>
        public string ServicePath { get; set; }

        /// <summary>
        /// Gets or sets the transport type to use to access the namespace.
        /// </summary>
        public TransportType TransportType { get; set; }

        /// <summary>
        /// Gets or sets the URL of the sts endpoint.
        /// </summary>
        public string StsEndpoint { get; set; }

        /// <summary>
        /// Gets or sets the runtime port.
        /// </summary>
        public string RuntimePort { get; set; }

        /// <summary>
        /// Gets or sets the management port.
        /// </summary>
        public string ManagementPort { get; set; }

        /// <summary>
        /// Gets or sets the windows domain.
        /// </summary>
        public string WindowsDomain { get; set; }

        /// <summary>
        /// Gets or sets the Windows user name.
        /// </summary>
        public string WindowsUserName { get; set; }

        /// <summary>
        /// Gets or sets the Windows user password.
        /// </summary>
        public string WindowsPassword { get; set; }

        /// <summary>
        /// Gets or sets the SharedAccessKeyName.
        /// </summary>
        public string SharedAccessKeyName { get; set; }

        /// <summary>
        /// Gets or sets the SharedAccessKey.
        /// </summary>
        public string SharedAccessKey { get; set; }

        /// <summary>
        /// Gets or sets the EntityPath
        /// </summary>
        public string EntityPath { get; set; }

        /// <summary>
        /// Gets or sets the fully-qualified namespace (e.g. "mybus.servicebus.windows.net").
        /// Used by Entra-ID-authenticated namespaces.
        /// </summary>
        public string FullyQualifiedNamespace { get; set; }

        /// <summary>
        /// Authentication options for Entra ID (managed identity, service principal, ...). 
        /// When <see cref="ConnectionStringType"/> is <see cref="ServiceBusNamespaceType.EntraId"/>
        /// this drives credential creation.
        /// </summary>
        public EntraIdAuthenticationOptions EntraIdAuthentication { get; set; }
        #endregion

        #region Private Methods

        static ServiceBusNamespace GetServiceBusNamespaceUsingWindows(string key, string connectionString,
            WriteToLogDelegate staticWriteToLog, bool isUserCreated, string toLower, 
            Dictionary<string, string> parameters)
        {
            if (!toLower.Contains(ConnectionStringEndpoint) ||
                !toLower.Contains(ConnectionStringStsEndpoint) ||
                !toLower.Contains(ConnectionStringRuntimePort) ||
                !toLower.Contains(ConnectionStringManagementPort))
            {
                return null;
            }

            var endpoint = parameters.ContainsKey(ConnectionStringEndpoint) ?
                           parameters[ConnectionStringEndpoint] :
                           null;

            if (string.IsNullOrWhiteSpace(endpoint))
            {
                staticWriteToLog(string.Format(CultureInfo.CurrentCulture, 
                    ServiceBusNamespaceEndpointIsNullOrEmpty, key));
                return null;
            }

            string ns = GetNamespaceNameFromEndpoint(endpoint, staticWriteToLog, key);  

            var stsEndpoint = parameters.ContainsKey(ConnectionStringStsEndpoint) ?
                              parameters[ConnectionStringStsEndpoint] :
                              null;

            if (string.IsNullOrWhiteSpace(stsEndpoint))
            {
                staticWriteToLog(string.Format(CultureInfo.CurrentCulture, 
                    ServiceBusNamespaceStsEndpointIsNullOrEmpty, key));
                return null;
            }

            var runtimePort = parameters.ContainsKey(ConnectionStringRuntimePort) ?
                              parameters[ConnectionStringRuntimePort] :
                              null;

            if (string.IsNullOrWhiteSpace(runtimePort))
            {
                staticWriteToLog(string.Format(CultureInfo.CurrentCulture, 
                    ServiceBusNamespaceRuntimePortIsNullOrEmpty, key));
                return null;
            }

            var managementPort = parameters.ContainsKey(ConnectionStringManagementPort) ?
                                 parameters[ConnectionStringManagementPort] :
                                 null;

            if (string.IsNullOrWhiteSpace(managementPort))
            {
                staticWriteToLog(string.Format(CultureInfo.CurrentCulture, ServiceBusNamespaceManagementPortIsNullOrEmpty, key));
                return null;
            }

            var windowsDomain = parameters.ContainsKey(ConnectionStringWindowsDomain) ?
                                parameters[ConnectionStringWindowsDomain] :
                                null;

            var windowsUsername = parameters.ContainsKey(ConnectionStringWindowsUsername) ?
                                  parameters[ConnectionStringWindowsUsername] :
                                  null;

            var windowsPassword = parameters.ContainsKey(ConnectionStringWindowsPassword) ?
                                  parameters[ConnectionStringWindowsPassword] :
                                  null;

            var settings = new MessagingFactorySettings();
            var transportType = settings.TransportType;

            if (parameters.ContainsKey(ConnectionStringTransportType))
            {
                Enum.TryParse(parameters[ConnectionStringTransportType], true, out transportType);
            }
            
            return new ServiceBusNamespace(connectionString, endpoint, stsEndpoint, runtimePort, managementPort, windowsDomain, windowsUsername, windowsPassword, ns, transportType, isUserCreated);
        }

        static ServiceBusNamespace GetServiceBusNamespaceUsingSAS(string key, string connectionString, 
            WriteToLogDelegate staticWriteToLog, bool isUserCreated, Dictionary<string, string> parameters)
        {
            if (parameters.Count < 3)
            {
                staticWriteToLog(string.Format(CultureInfo.CurrentCulture, ServiceBusNamespaceIsWrong, key));
                return null;
            }

            var endpoint = parameters.ContainsKey(ConnectionStringEndpoint) ?
                           parameters[ConnectionStringEndpoint] :
                           null;

            if (string.IsNullOrWhiteSpace(endpoint))
            {
                staticWriteToLog(string.Format(CultureInfo.CurrentCulture, 
                    ServiceBusNamespaceEndpointIsNullOrEmpty, key));
                return null;
            }

            if (!endpoint.Contains("://"))
            {
                staticWriteToLog(string.Format(CultureInfo.CurrentCulture, 
                    ServiceBusNamespaceEndpointPrefixedWithSb, endpoint));
                endpoint = "sb://" + endpoint;
            }

            var stsEndpoint = parameters.ContainsKey(ConnectionStringStsEndpoint) ?
                              parameters[ConnectionStringStsEndpoint] :
                              null;

            string ns = GetNamespaceNameFromEndpoint(endpoint, staticWriteToLog, key);           

            if (!parameters.ContainsKey(ConnectionStringSharedAccessKeyName) || 
                string.IsNullOrWhiteSpace(parameters[ConnectionStringSharedAccessKeyName]))
            {
                staticWriteToLog(string.Format(CultureInfo.CurrentCulture, ServiceBusNamespaceSharedAccessKeyNameIsInvalid, key));
            }

            var sharedAccessKeyName = parameters[ConnectionStringSharedAccessKeyName];

            if (!parameters.ContainsKey(ConnectionStringSharedAccessKey) || string.IsNullOrWhiteSpace(parameters[ConnectionStringSharedAccessKey]))
            {
                staticWriteToLog(string.Format(CultureInfo.CurrentCulture, 
                    ServiceBusNamespaceSharedAccessKeyIsInvalid, key));
            }

            var sharedAccessKey = parameters[ConnectionStringSharedAccessKey];
            var settings = new MessagingFactorySettings();
            var transportType = settings.TransportType;

            if (parameters.ContainsKey(ConnectionStringTransportType))
            {
                Enum.TryParse(parameters[ConnectionStringTransportType], true, out transportType);
            }

            string entityPath = string.Empty;

            if (parameters.ContainsKey(ConnectionStringEntityPath))
            {
                entityPath = parameters[ConnectionStringEntityPath];
            }

            return new ServiceBusNamespace(ServiceBusNamespaceType.Cloud, connectionString, endpoint, ns, null,
                sharedAccessKeyName, sharedAccessKey, stsEndpoint, transportType, true,
                entityPath, isUserCreated);
        }

        static string GetNamespaceNameFromEndpoint(string endpoint, WriteToLogDelegate staticWriteToLog, 
            string key)
        {
             Uri uri;

            try
            {
                uri = new Uri(endpoint);
            }
            catch (UriFormatException)
            {
                staticWriteToLog(string.Format(CultureInfo.CurrentCulture, 
                    ServiceBusNamespaceEndpointUriIsInvalid, key));
                return null;
            }

            return uri.Host.Split('.')[0];
        }

        static ServiceBusNamespace GetServiceBusNamespaceUsingEntraId(string key, string connectionString,
            WriteToLogDelegate staticWriteToLog, bool isUserCreated, Dictionary<string, string> parameters)
        {
            var endpoint = parameters.ContainsKey(ConnectionStringEndpoint)
                ? parameters[ConnectionStringEndpoint]
                : null;

            if (string.IsNullOrWhiteSpace(endpoint))
            {
                staticWriteToLog(string.Format(CultureInfo.CurrentCulture,
                    ServiceBusNamespaceEndpointIsNullOrEmpty, key));
                return null;
            }

            if (!endpoint.Contains("://"))
            {
                staticWriteToLog(string.Format(CultureInfo.CurrentCulture,
                    ServiceBusNamespaceEndpointPrefixedWithSb, endpoint));
                endpoint = "sb://" + endpoint;
            }

            Uri uri;
            try
            {
                uri = new Uri(endpoint);
            }
            catch (Exception)
            {
                staticWriteToLog(string.Format(CultureInfo.CurrentCulture,
                    ServiceBusNamespaceEndpointUriIsInvalid, key));
                return null;
            }

            var fqdn = uri.Host;
            var authValue = parameters.ContainsKey(ConnectionStringAuthentication)
                ? parameters[ConnectionStringAuthentication]
                : null;

            var mode = ParseAuthenticationMode(authValue);
            if (mode == AuthenticationMode.Sas)
            {
                staticWriteToLog(string.Format(CultureInfo.CurrentCulture,
                    "Authentication value '{0}' for namespace {1} is not recognized.", authValue, key));
                return null;
            }

            var options = new EntraIdAuthenticationOptions
            {
                Mode = mode,
                ClientId = parameters.ContainsKey(ConnectionStringClientId) ? parameters[ConnectionStringClientId] : null,
                TenantId = parameters.ContainsKey(ConnectionStringTenantId) ? parameters[ConnectionStringTenantId] : null,
                ClientSecret = parameters.ContainsKey(ConnectionStringClientSecret) ? parameters[ConnectionStringClientSecret] : null,
                CertificateThumbprint = parameters.ContainsKey(ConnectionStringCertificateThumbprint) ? parameters[ConnectionStringCertificateThumbprint] : null,
            };

            var settings = new MessagingFactorySettings();
            var transportType = settings.TransportType;
            if (parameters.ContainsKey(ConnectionStringTransportType))
            {
                Enum.TryParse(parameters[ConnectionStringTransportType], true, out transportType);
            }

            var entityPath = parameters.ContainsKey(ConnectionStringEntityPath)
                ? parameters[ConnectionStringEntityPath]
                : string.Empty;

            return new ServiceBusNamespace(fqdn, options, transportType, entityPath, isUserCreated)
            {
                ConnectionString = connectionString,
            };
        }

        public static AuthenticationMode ParseAuthenticationMode(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return AuthenticationMode.Sas;
            }

            var normalized = new string(value.Where(char.IsLetterOrDigit).ToArray()).ToLowerInvariant();

            switch (normalized)
            {
                case "managedidentity":
                case "msi":
                case "managedidentitysystemassigned":
                case "systemassignedmanagedidentity":
                    return AuthenticationMode.ManagedIdentitySystemAssigned;
                case "managedidentityuserassigned":
                case "userassignedmanagedidentity":
                    return AuthenticationMode.ManagedIdentityUserAssigned;
                case "defaultazurecredential":
                case "default":
                case "developer":
                    return AuthenticationMode.DefaultAzureCredential;
                case "serviceprincipal":
                case "serviceprincipalsecret":
                case "clientsecret":
                    return AuthenticationMode.ServicePrincipalSecret;
                case "serviceprincipalcertificate":
                case "clientcertificate":
                case "certificate":
                    return AuthenticationMode.ServicePrincipalCertificate;
                case "interactivebrowser":
                case "interactive":
                case "browser":
                    return AuthenticationMode.InteractiveBrowser;
                default:
                    return AuthenticationMode.Sas;
            }
        }

        public static string FormatAuthenticationMode(AuthenticationMode mode)
        {
            switch (mode)
            {
                case AuthenticationMode.ManagedIdentitySystemAssigned:
                    return "Managed Identity";
                case AuthenticationMode.ManagedIdentityUserAssigned:
                    return "Managed Identity (User Assigned)";
                case AuthenticationMode.DefaultAzureCredential:
                    return "DefaultAzureCredential";
                case AuthenticationMode.ServicePrincipalSecret:
                    return "Service Principal";
                case AuthenticationMode.ServicePrincipalCertificate:
                    return "Service Principal Certificate";
                case AuthenticationMode.InteractiveBrowser:
                    return "Interactive Browser";
                default:
                    return "SAS";
            }
        }

        public static string BuildEntraIdConnectionString(string fullyQualifiedNamespace,
            EntraIdAuthenticationOptions options, TransportType transportType, string entityPath = "")
        {
            if (options == null)
            {
                throw new ArgumentNullException(nameof(options));
            }

            var fqdn = NormalizeFqdn(fullyQualifiedNamespace);
            var endpoint = $"sb://{fqdn}/";
            var sb = new System.Text.StringBuilder();
            sb.Append("Endpoint=").Append(endpoint).Append(";");
            sb.Append("Authentication=").Append(FormatAuthenticationMode(options.Mode)).Append(";");

            if (!string.IsNullOrWhiteSpace(options.ClientId))
            {
                sb.Append("ClientId=").Append(options.ClientId).Append(";");
            }

            if (!string.IsNullOrWhiteSpace(options.TenantId))
            {
                sb.Append("TenantId=").Append(options.TenantId).Append(";");
            }

            if (options.Mode == AuthenticationMode.ServicePrincipalSecret &&
                !string.IsNullOrWhiteSpace(options.ClientSecret))
            {
                sb.Append("ClientSecret=").Append(options.ClientSecret).Append(";");
            }

            if (options.Mode == AuthenticationMode.ServicePrincipalCertificate &&
                !string.IsNullOrWhiteSpace(options.CertificateThumbprint))
            {
                sb.Append("CertificateThumbprint=").Append(options.CertificateThumbprint).Append(";");
            }

            sb.Append("TransportType=").Append(transportType.ToString());

            if (!string.IsNullOrWhiteSpace(entityPath))
            {
                sb.Append(";EntityPath=").Append(entityPath);
            }

            return sb.ToString();
        }

        static string NormalizeFqdn(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return value;
            }

            var trimmed = value.Trim();
            if (trimmed.Contains("://"))
            {
                try
                {
                    var uri = new Uri(trimmed);
                    return uri.Host;
                }
                catch
                {
                    // fall through
                }
            }

            return trimmed.Trim('/');
        }

        #endregion
    }
}
