using System.Text.Json.Nodes;

namespace Cauce.Api.IntegrationTests.Qa.Support;

/// <summary>
/// Arma manifiestos de QA de prueba. Cada cuenta queda en uso por los casos que la nombran.
/// </summary>
public sealed class QaManifestBuilder
{
    private readonly List<(string Email, string? Role, string Disposition, string? Status, string? DisplayName)> _accounts = [];
    private readonly List<(string Cp, string[] Accounts, string[] MustNotExist, string Seeds)> _cases = [];

    public QaManifestBuilder Patient(string email, string status = "active", string? displayName = null)
    {
        _accounts.Add((email, "patient", "seed", status, displayName));
        return this;
    }

    public QaManifestBuilder Nutritionist(string email, string status = "active", string? displayName = null)
    {
        _accounts.Add((email, "nutritionist", "seed", status, displayName));
        return this;
    }

    public QaManifestBuilder MustNotExist(string email)
    {
        _accounts.Add((email, null, "mustNotExist", null, null));
        return this;
    }

    public QaManifestBuilder Case(string cp, string[] accounts, string seeds = "[]", string[]? mustNotExist = null)
    {
        _cases.Add((cp, accounts, mustNotExist ?? [], seeds));
        return this;
    }

    public string Build()
    {
        var accounts = new JsonArray();
        foreach (var account in _accounts)
        {
            var usedBy = new JsonArray(_cases
                .Where(testCase => testCase.Accounts.Contains(account.Email) || testCase.MustNotExist.Contains(account.Email))
                .Select(testCase => (JsonNode)JsonValue.Create(testCase.Cp))
                .ToArray());
            accounts.Add(new JsonObject
            {
                ["email"] = account.Email,
                ["role"] = account.Role,
                ["disposition"] = account.Disposition,
                ["status"] = account.Status,
                ["displayName"] = account.DisplayName,
                ["usedBy"] = usedBy
            });
        }

        var cases = new JsonArray();
        foreach (var testCase in _cases)
        {
            cases.Add(new JsonObject
            {
                ["cp"] = testCase.Cp,
                ["accounts"] = new JsonArray(testCase.Accounts.Select(email => (JsonNode)JsonValue.Create(email)).ToArray()),
                ["mustNotExist"] = new JsonArray(testCase.MustNotExist.Select(email => (JsonNode)JsonValue.Create(email)).ToArray()),
                ["consumed"] = true,
                ["clock"] = null,
                ["seed"] = JsonNode.Parse(testCase.Seeds)
            });
        }

        return new JsonObject
        {
            ["manifestVersion"] = "1.1-draft2",
            ["accounts"] = accounts,
            ["cases"] = cases
        }.ToJsonString();
    }
}
