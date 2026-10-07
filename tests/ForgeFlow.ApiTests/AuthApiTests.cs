using System.Net;
using ForgeFlow.Application.Auth;
using ForgeFlow.Domain.Users;
using ForgeFlow.Infrastructure.Persistence;

namespace ForgeFlow.ApiTests;

public sealed class AuthApiTests(ForgeFlowApiFactory factory) : IClassFixture<ForgeFlowApiFactory>
{
    [Fact]
    public async Task Login_WithDemoCredentials_ReturnsTokenAndRole()
    {
        var client = factory.CreateClient();

        var response = await client.PostJsonAsync("/api/auth/login",
            new LoginRequest { Email = ForgeFlowApiFactory.Engineer, Password = DemoDataSeeder.DemoPassword });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.ReadAsAsync<LoginResponse>();
        Assert.False(string.IsNullOrWhiteSpace(body.AccessToken));
        Assert.Equal(UserRole.Engineer, body.User.Role);
        Assert.True(body.ExpiresAtUtc > DateTime.UtcNow);
    }

    [Fact]
    public async Task Login_WithWrongPassword_Returns401()
    {
        var client = factory.CreateClient();

        var response = await client.PostJsonAsync("/api/auth/login",
            new LoginRequest { Email = ForgeFlowApiFactory.Engineer, Password = "WrongPassword1" });

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Login_WithUnknownEmail_Returns401()
    {
        var client = factory.CreateClient();

        var response = await client.PostJsonAsync("/api/auth/login",
            new LoginRequest { Email = "nobody@forgeflow.local", Password = DemoDataSeeder.DemoPassword });

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task ProtectedEndpoint_WithoutToken_Returns401()
    {
        var client = factory.CreateClient();

        var response = await client.GetAsync("/api/products");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Me_ReturnsSignedInUser()
    {
        var client = await factory.CreateClientForAsync(ForgeFlowApiFactory.Approver);

        var me = await (await client.GetAsync("/api/auth/me")).ReadAsAsync<UserInfo>();

        Assert.Equal(ForgeFlowApiFactory.Approver, me.Email);
        Assert.Equal(UserRole.Approver, me.Role);
    }
}
