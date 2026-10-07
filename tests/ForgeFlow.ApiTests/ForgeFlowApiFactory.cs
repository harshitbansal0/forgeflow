using System.Collections.Concurrent;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using ForgeFlow.Application.Auth;
using ForgeFlow.Infrastructure.Persistence;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Configuration;

namespace ForgeFlow.ApiTests;

/// <summary>Hosts the real API against a throwaway SQLite file seeded with the demo data.</summary>
public sealed class ForgeFlowApiFactory : WebApplicationFactory<Program>
{
    public const string Admin = "admin@forgeflow.local";
    public const string Engineer = "engineer@forgeflow.local";
    public const string Approver = "approver@forgeflow.local";
    public const string Viewer = "viewer@forgeflow.local";

    public static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() }
    };

    private readonly string _databasePath = Path.Combine(Path.GetTempPath(), $"forgeflow-api-tests-{Guid.NewGuid():N}.db");
    private readonly ConcurrentDictionary<string, string> _tokens = new();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.ConfigureAppConfiguration((_, configuration) => configuration.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Database:Provider"] = "Sqlite",
            ["ConnectionStrings:Sqlite"] = $"Data Source={_databasePath}",
            ["Jwt:SigningKey"] = "forgeflow-api-tests-signing-key-0123456789abcdef",
            ["Seeding:DemoData"] = "true",
            ["RateLimiting:LoginPermitLimit"] = "1000"
        }));
    }

    public async Task<HttpClient> CreateClientForAsync(string email)
    {
        var client = CreateClient();
        if (!_tokens.TryGetValue(email, out var token))
        {
            var response = await client.PostAsJsonAsync("/api/auth/login", new LoginRequest { Email = email, Password = DemoDataSeeder.DemoPassword });
            response.EnsureSuccessStatusCode();
            token = (await response.Content.ReadFromJsonAsync<LoginResponse>(Json))!.AccessToken;
            _tokens[email] = token;
        }

        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return client;
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        SqliteConnection.ClearAllPools();
        if (File.Exists(_databasePath))
        {
            File.Delete(_databasePath);
        }
    }
}

public static class HttpContentExtensions
{
    public static async Task<T> ReadAsAsync<T>(this HttpResponseMessage response) =>
        (await response.Content.ReadFromJsonAsync<T>(ForgeFlowApiFactory.Json))!;

    public static Task<HttpResponseMessage> PostJsonAsync<T>(this HttpClient client, string url, T body) =>
        client.PostAsJsonAsync(url, body, ForgeFlowApiFactory.Json);

    public static string UniqueNumber(string prefix) => $"{prefix}-{Guid.NewGuid():N}"[..Math.Min(prefix.Length + 13, 40)].ToUpperInvariant();
}
