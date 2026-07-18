using System.Linq;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using TaskFlow.Api.Dtos;
using TaskFlow.Api.Tests.TestSupport;

namespace TaskFlow.Api.Tests.Integration;

public class UsersEndpointsTests(TaskFlowApiFactory factory) : IClassFixture<TaskFlowApiFactory>
{
    private readonly HttpClient _client = factory.CreateClient();

    // TaskFlowApiFactory seeds exactly this Admin (see its Admin:Email/Admin:Password
    // configuration) — every test in this class shares that one host/database, so this
    // remains the sole Admin unless a test explicitly promotes someone else.
    private async Task<string> LoginAsSeededAdminAsync()
    {
        var response = await _client.PostAsJsonAsync(
            "/api/auth/login", new { email = "admin@taskflow.local", password = "IntegrationTest!Admin1" });
        var body = await response.Content.ReadFromJsonAsync<AuthResponse>();
        return body!.Token;
    }

    private async Task<string> RegisterAndGetTokenAsync(string email)
    {
        var response = await _client.PostAsJsonAsync(
            "/api/auth/register", new { fullName = "Regular User", email, password = "Password1" });
        var body = await response.Content.ReadFromJsonAsync<AuthResponse>();
        return body!.Token;
    }

    private static HttpRequestMessage AuthedRequest(HttpMethod method, string url, string token)
    {
        var request = new HttpRequestMessage(method, url);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return request;
    }

    private async Task<int> FindUserIdByEmailAsync(string adminToken, string email)
    {
        var response = await _client.SendAsync(AuthedRequest(HttpMethod.Get, "/api/users?page=1&pageSize=200", adminToken));
        var body = await response.Content.ReadFromJsonAsync<PagedResult<UserListItemDto>>();
        return body!.Items.Single(u => u.Email == email).Id;
    }

    [Fact]
    public async Task GetUsers_returns_200_with_a_paginated_list_for_an_admin_caller()
    {
        var adminToken = await LoginAsSeededAdminAsync();

        var response = await _client.SendAsync(AuthedRequest(HttpMethod.Get, "/api/users", adminToken));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<PagedResult<UserListItemDto>>();
        Assert.NotNull(body);
        Assert.Contains(body!.Items, u => u.Email == "admin@taskflow.local");
    }

    [Fact]
    public async Task GetUsers_returns_403_for_a_non_admin_caller()
    {
        var token = await RegisterAndGetTokenAsync($"nonadmin-{Guid.NewGuid():N}@example.com");

        var response = await _client.SendAsync(AuthedRequest(HttpMethod.Get, "/api/users", token));

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task ChangeRole_returns_200_and_updates_the_role_for_an_admin_caller()
    {
        var adminToken = await LoginAsSeededAdminAsync();
        var email = $"promote-{Guid.NewGuid():N}@example.com";
        await RegisterAndGetTokenAsync(email);
        var userId = await FindUserIdByEmailAsync(adminToken, email);

        var request = AuthedRequest(HttpMethod.Put, $"/api/users/{userId}/role", adminToken);
        request.Content = JsonContent.Create(new { role = "Manager" });
        var response = await _client.SendAsync(request);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<UserListItemDto>();
        Assert.Equal("Manager", body!.Role);
    }

    [Fact]
    public async Task ChangeRole_returns_403_for_a_non_admin_caller()
    {
        var token = await RegisterAndGetTokenAsync($"nonadmin-role-{Guid.NewGuid():N}@example.com");

        var request = AuthedRequest(HttpMethod.Put, "/api/users/1/role", token);
        request.Content = JsonContent.Create(new { role = "Manager" });
        var response = await _client.SendAsync(request);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task ChangeRole_returns_404_for_an_unknown_user_id()
    {
        var adminToken = await LoginAsSeededAdminAsync();

        var request = AuthedRequest(HttpMethod.Put, "/api/users/999999/role", adminToken);
        request.Content = JsonContent.Create(new { role = "Manager" });
        var response = await _client.SendAsync(request);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task ChangeRole_returns_400_when_the_last_admin_tries_to_self_demote()
    {
        var adminToken = await LoginAsSeededAdminAsync();
        var adminId = await FindUserIdByEmailAsync(adminToken, "admin@taskflow.local");

        var request = AuthedRequest(HttpMethod.Put, $"/api/users/{adminId}/role", adminToken);
        request.Content = JsonContent.Create(new { role = "Developer" });
        var response = await _client.SendAsync(request);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    // Unlike GetUsers/ChangeRole above (still 403 for non-Admins — regression-checked by
    // the two tests above, which must keep passing), this lookup endpoint is deliberately
    // open to any authenticated role (research.md §9, Feature 002).
    [Fact]
    public async Task GetLookup_returns_200_for_a_non_admin_caller()
    {
        var token = await RegisterAndGetTokenAsync($"lookup-nonadmin-{Guid.NewGuid():N}@example.com");

        var response = await _client.SendAsync(AuthedRequest(HttpMethod.Get, "/api/users/lookup", token));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task GetLookup_returns_401_without_a_token()
    {
        var response = await _client.GetAsync("/api/users/lookup");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }
}
