using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using TaskFlow.Api.Tests.TestSupport;

namespace TaskFlow.Api.Tests.Integration;

// Validates SC-005 and constitution Principle II directly: asserts the *property is
// absent* from the serialized JSON, not merely that it's null — a DTO that carried a
// PasswordHash field which happened to be empty would still pass a "is it null" check
// but fail this one, which is the actual guarantee FR-006 requires.
public class PasswordExposureTests(TaskFlowApiFactory factory) : IClassFixture<TaskFlowApiFactory>
{
    private readonly HttpClient _client = factory.CreateClient();

    private static void AssertNoPasswordProperty(JsonElement element)
    {
        switch (element.ValueKind)
        {
            case JsonValueKind.Object:
                foreach (var property in element.EnumerateObject())
                {
                    Assert.False(
                        property.Name.Contains("password", StringComparison.OrdinalIgnoreCase),
                        $"Response contains a password-related property: '{property.Name}'");
                    AssertNoPasswordProperty(property.Value);
                }
                break;
            case JsonValueKind.Array:
                foreach (var item in element.EnumerateArray())
                {
                    AssertNoPasswordProperty(item);
                }
                break;
        }
    }

    private async Task<string> RegisterAndGetTokenAsync(string email)
    {
        var response = await _client.PostAsJsonAsync(
            "/api/auth/register", new { fullName = "Password Audit", email, password = "Password1" });
        var json = await response.Content.ReadFromJsonAsync<JsonElement>();
        return json.GetProperty("token").GetString()!;
    }

    private static HttpRequestMessage AuthedRequest(HttpMethod method, string url, string token)
    {
        var request = new HttpRequestMessage(method, url);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return request;
    }

    [Fact]
    public async Task Register_response_never_exposes_a_password_or_hash()
    {
        var response = await _client.PostAsJsonAsync("/api/auth/register", new
        {
            fullName = "Password Audit",
            email = $"pwaudit-register-{Guid.NewGuid():N}@example.com",
            password = "Password1"
        });

        var json = await response.Content.ReadFromJsonAsync<JsonElement>();
        AssertNoPasswordProperty(json);
    }

    [Fact]
    public async Task Login_response_never_exposes_a_password_or_hash()
    {
        var email = $"pwaudit-login-{Guid.NewGuid():N}@example.com";
        await _client.PostAsJsonAsync("/api/auth/register", new { fullName = "Password Audit", email, password = "Password1" });

        var response = await _client.PostAsJsonAsync("/api/auth/login", new { email, password = "Password1" });

        var json = await response.Content.ReadFromJsonAsync<JsonElement>();
        AssertNoPasswordProperty(json);
    }

    [Fact]
    public async Task Me_response_never_exposes_a_password_or_hash()
    {
        var token = await RegisterAndGetTokenAsync($"pwaudit-me-{Guid.NewGuid():N}@example.com");

        var response = await _client.SendAsync(AuthedRequest(HttpMethod.Get, "/api/auth/me", token));

        var json = await response.Content.ReadFromJsonAsync<JsonElement>();
        AssertNoPasswordProperty(json);
    }

    [Fact]
    public async Task GetUsers_response_never_exposes_a_password_or_hash()
    {
        var loginResponse = await _client.PostAsJsonAsync(
            "/api/auth/login", new { email = "admin@taskflow.local", password = "IntegrationTest!Admin1" });
        var adminToken = (await loginResponse.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("token").GetString()!;
        await RegisterAndGetTokenAsync($"pwaudit-list-{Guid.NewGuid():N}@example.com");

        var response = await _client.SendAsync(AuthedRequest(HttpMethod.Get, "/api/users?page=1&pageSize=200", adminToken));

        var json = await response.Content.ReadFromJsonAsync<JsonElement>();
        AssertNoPasswordProperty(json);
    }
}
