using System.Net;
using System.Net.Http.Json;
using ClinicDesk.Api.Application.Dtos;
using ClinicDesk.Api.Tests.Infrastructure;

namespace ClinicDesk.Api.Tests.Integration;

public class AuthEndpointsTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly CustomWebApplicationFactory _factory;

    public AuthEndpointsTests(CustomWebApplicationFactory factory)
    {
        _factory = factory;
    }

    private static RegisterPatientRequest NewPatientRequest(string email) =>
        new(email, "Sup3rSecreta!", "Paciente de Prueba", "5555555555", new DateOnly(1995, 5, 20));

    [Fact]
    public async Task Register_ThenLogin_ReturnsToken()
    {
        var client = _factory.CreateClient();
        var email = $"{Guid.NewGuid()}@clinicdesk.test";

        var registerResponse = await client.PostAsJsonAsync("/api/auth/register", NewPatientRequest(email));
        registerResponse.EnsureSuccessStatusCode();
        var registered = await registerResponse.Content.ReadFromJsonAsync<AuthResponse>();
        Assert.False(string.IsNullOrWhiteSpace(registered!.Token));
        Assert.Equal("Patient", registered.Role);

        var loginResponse = await client.PostAsJsonAsync("/api/auth/login", new LoginRequest(email, "Sup3rSecreta!"));
        loginResponse.EnsureSuccessStatusCode();
        var loggedIn = await loginResponse.Content.ReadFromJsonAsync<AuthResponse>();
        Assert.False(string.IsNullOrWhiteSpace(loggedIn!.Token));
    }

    [Fact]
    public async Task Register_WithDuplicateEmail_Returns409Problem()
    {
        var client = _factory.CreateClient();
        var email = $"{Guid.NewGuid()}@clinicdesk.test";
        await client.PostAsJsonAsync("/api/auth/register", NewPatientRequest(email));

        var secondResponse = await client.PostAsJsonAsync("/api/auth/register", NewPatientRequest(email));

        Assert.Equal(HttpStatusCode.Conflict, secondResponse.StatusCode);
        Assert.Equal("application/problem+json", secondResponse.Content.Headers.ContentType?.MediaType);
    }

    [Fact]
    public async Task Login_WithWrongPassword_Returns401()
    {
        var client = _factory.CreateClient();
        var email = $"{Guid.NewGuid()}@clinicdesk.test";
        await client.PostAsJsonAsync("/api/auth/register", NewPatientRequest(email));

        var response = await client.PostAsJsonAsync("/api/auth/login", new LoginRequest(email, "ClaveIncorrecta!"));

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task ProtectedEndpoint_WithoutToken_Returns401()
    {
        var client = _factory.CreateClient();

        var response = await client.GetAsync("/api/appointments");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task GetUnknownAppointment_Returns404Problem()
    {
        var client = _factory.CreateClient();
        var email = $"{Guid.NewGuid()}@clinicdesk.test";
        var registerResponse = await client.PostAsJsonAsync("/api/auth/register", NewPatientRequest(email));
        var registered = await registerResponse.Content.ReadFromJsonAsync<AuthResponse>();
        client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", registered!.Token);

        var response = await client.GetAsync($"/api/appointments/{Guid.NewGuid()}");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
    }
}
