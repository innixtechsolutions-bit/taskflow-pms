using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using TaskFlow.Api.Tests.TestSupport;

namespace TaskFlow.Api.Tests.Integration;

// Simulates the real dev launch profile (dual http+https endpoints bound
// simultaneously) without needing real socket bindings or a trusted dev
// certificate: HttpsRedirectionMiddleware also honors an explicit "https_port"
// configuration value — the same mechanism ASPNETCORE_HTTPS_PORT feeds in via
// launchSettings.json's dual-endpoint "https" profile — which is enough to
// reproduce its redirect decision under the in-memory TestServer.
public class HttpsPortConfiguredFactory : TaskFlowApiFactory
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        base.ConfigureWebHost(builder);
        builder.ConfigureAppConfiguration((_, config) =>
            config.AddInMemoryCollection(new Dictionary<string, string?> { ["https_port"] = "7024" }));
    }
}

// Bug report: after a successful login, clicking "Projects" redirected back to
// /login even though the visitor was authenticated. Root cause: the Angular dev
// server's proxy targets http://localhost:5146 only (frontend/proxy.conf.json),
// but UseHttpsRedirection() unconditionally 307-redirects every plain-HTTP
// request to the https endpoint whenever the backend is run under its default
// dual-port launch profile. The browser follows that redirect directly —
// bypassing the dev-server proxy and crossing origins
// (http://localhost:4300 -> https://localhost:7024) — and strips the
// Authorization header on that cross-origin hop (Fetch spec behavior), so the
// redirected request arrives unauthenticated and gets a genuine 401, which the
// frontend's interceptor then turns into a forced logout. This middleware has
// been unconditional since Feature 001; Feature 003's route changes did not
// cause this — it just hadn't been triggered by a QA session using the
// dual-port profile until now.
public class HttpsRedirectionTests(HttpsPortConfiguredFactory factory) : IClassFixture<HttpsPortConfiguredFactory>
{
    [Fact]
    public async Task Login_over_plain_HTTP_is_not_redirected_to_HTTPS_in_Development()
    {
        var client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

        var response = await client.PostAsJsonAsync(
            "/api/auth/login", new { email = "admin@taskflow.local", password = "IntegrationTest!Admin1" });

        Assert.NotEqual(HttpStatusCode.TemporaryRedirect, response.StatusCode);
    }
}
