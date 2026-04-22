#region Copyright
//=======================================================================================
// Copyright (c) Microsoft Corporation. All rights reserved.
//=======================================================================================
#endregion

namespace ServiceBusExplorer.Auth
{
    public enum AuthenticationMode
    {
        Sas = 0,
        ManagedIdentitySystemAssigned = 1,
        ManagedIdentityUserAssigned = 2,
        DefaultAzureCredential = 3,
        ServicePrincipalSecret = 4,
        ServicePrincipalCertificate = 5,
        InteractiveBrowser = 6,
    }
}
