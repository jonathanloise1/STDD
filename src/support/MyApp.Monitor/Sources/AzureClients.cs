using Azure.Core;
using Azure.Identity;
using Azure.Monitor.Query;
using Azure.ResourceManager;
using Azure.ResourceManager.Resources;
using MyApp.Monitor.Tools;

namespace MyApp.Monitor.Sources;

/// <summary>Credential chain of operations.md 6.3. Never keys or connection strings.</summary>
public static class AzureCredentialFactory
{
    public static TokenCredential Create(MonitorOptions options)
    {
        options.ValidateAzure();
        if (options.Azure.Credential == "managed_identity")
        {
            var clientId = options.Azure.ManagedIdentityClientId;
            return string.IsNullOrWhiteSpace(clientId)
                ? new ManagedIdentityCredential(ManagedIdentityId.SystemAssigned)
                : new ManagedIdentityCredential(ManagedIdentityId.FromUserAssignedClientId(clientId));
        }
        return new ChainedTokenCredential(new AzureCliCredential(), new VisualStudioCredential(), new AzureDeveloperCliCredential());
    }
}

/// <summary>The real <see cref="ILogAnalyticsClient"/> (Azure.Monitor.Query). Not exercised by the tests: they use a fake.</summary>
public sealed class AzureLogAnalyticsClient(TokenCredential credential) : ILogAnalyticsClient
{
    private readonly LogsQueryClient _client = new(credential);

    public async Task<IReadOnlyList<IReadOnlyDictionary<string, object?>>> QueryAsync(string workspace, string query, CancellationToken ct)
    {
        var response = await _client.QueryWorkspaceAsync(workspace, query, QueryTimeRange.All, cancellationToken: ct).ConfigureAwait(false);
        var table = response.Value.Table;
        var names = table.Columns.Select(c => c.Name).ToList();
        var rows = new List<IReadOnlyDictionary<string, object?>>();
        foreach (var row in table.Rows)
        {
            var dict = new Dictionary<string, object?>(StringComparer.Ordinal);
            for (var i = 0; i < names.Count; i++)
            {
                var value = row[i];
                dict[names[i]] = value is BinaryData bd ? bd.ToString() : value;
            }
            rows.Add(dict);
        }
        return rows;
    }
}

/// <summary>The real <see cref="IWorkspaceDiscoveryClient"/> (Azure.ResourceManager): resources of one type with a tag, per subscription.</summary>
public sealed class AzureWorkspaceDiscoveryClient(TokenCredential credential) : IWorkspaceDiscoveryClient
{
    public async Task<IReadOnlyList<string>> FindWorkspacesAsync(
        string tagName, string tagValue, string resourceType, IReadOnlyList<string> subscriptionIds, CancellationToken ct)
    {
        var arm = new ArmClient(credential);
        var subscriptions = new List<SubscriptionResource>();
        if (subscriptionIds.Count == 0)
        {
            await foreach (var s in arm.GetSubscriptions().GetAllAsync(ct).ConfigureAwait(false)) subscriptions.Add(s);
        }
        else
        {
            foreach (var id in subscriptionIds)
                subscriptions.Add(arm.GetSubscriptionResource(SubscriptionResource.CreateResourceIdentifier(id)));
        }

        var filter = $"resourceType eq '{Escape(resourceType)}' and tagName eq '{Escape(tagName)}' and tagValue eq '{Escape(tagValue)}'";
        var found = new List<string>();
        foreach (var subscription in subscriptions)
            await foreach (var resource in subscription.GetGenericResourcesAsync(filter, null, null, ct).ConfigureAwait(false))
                found.Add(resource.Id.ToString());
        return found.Distinct(StringComparer.OrdinalIgnoreCase).ToList();
    }

    private static string Escape(string s) => s.Replace("'", "''", StringComparison.Ordinal);
}
