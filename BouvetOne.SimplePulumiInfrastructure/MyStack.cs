using System.Collections.Generic;
using Pulumi;
using Pulumi.Azure.AppService;
using Pulumi.Azure.AppService.Inputs;
using Pulumi.Azure.Core;
using Pulumi.Azure.KeyVault;
using Pulumi.Azure.Storage;

internal class MyStack : Stack
{
    public MyStack()
    {
        // Create an Azure Resource Group
        var resourceGroup = new ResourceGroup("bv-one-demo-dev-rg");

        // Create an Azure Storage Account
        var storageAccount = new Account("bvfagdemodevsa", new AccountArgs
        {
            ResourceGroupName = resourceGroup.Name,
            AccountReplicationType = "LRS",
            AccountTier = "Standard",
        });

        // Export the connection string for the storage account
        this.ConnectionString = storageAccount.PrimaryConnectionString;

        var current = Output.Create(Pulumi.Azure.Core.GetClientConfig.InvokeAsync());
        var kv = new KeyVault("bv-demo-dev-kv", new KeyVaultArgs()
        {
            ResourceGroupName = resourceGroup.Name,
            SkuName = "standard",
            TenantId = current.Apply(current => current.TenantId),
            SoftDeleteEnabled = true,
            SoftDeleteRetentionDays = 7
        });

        var self = new AccessPolicy("self", new AccessPolicyArgs()
        {
            ObjectId = current.Apply(x => x.ObjectId),
            SecretPermissions = new InputList<string>() { "get", "set", "list", "delete" },
            TenantId = kv.TenantId,
            KeyVaultId = kv.Id,
        });

        var plan = new Plan("bv-fag-demo-dev-plan", new PlanArgs()
        {
            ResourceGroupName = resourceGroup.Name,
            Sku = new PlanSkuArgs()
            {
                Size = "P1v2",
                Tier = "PremiumV2"
            },
        });

        var appServiceArgs = new AppServiceArgs()
        {
            AppServicePlanId = plan.Id,
            ResourceGroupName = resourceGroup.Name,
            Identity = new AppServiceIdentityArgs()
            {
                Type = "SystemAssigned"
            },
            AppSettings = new InputMap<string>()
        };

        appServiceArgs.AppSettings.Add("KeyVault_VaultUri", kv.VaultUri);

        var appService = new AppService("bv-fag-demo-dev-app", appServiceArgs);

        var ap = new AccessPolicy("web-app-access", new AccessPolicyArgs()
        {
            ObjectId = appService.Identity.Apply(x => x.PrincipalId ?? "11111111-1111-1111-1111-111111111111"),
            SecretPermissions = new InputList<string>() { "get", "list" },
            TenantId = kv.TenantId,
            KeyVaultId = kv.Id,
        });

        var secret = new Secret("kv-secret", new SecretArgs()
        {
            Name = "kv-secret",
            KeyVaultId = kv.Id,
            Value = storageAccount.PrimaryConnectionString,
        }, new CustomResourceOptions() { DependsOn = new List<Resource>() { self } });
    }

    [Output]
    public Output<string> ConnectionString { get; set; }
}
