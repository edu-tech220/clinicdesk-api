using System.Net;
using System.Net.Http.Json;
using ClinicDesk.Api.Application.Dtos;
using ClinicDesk.Api.Tests.Infrastructure;

namespace ClinicDesk.Api.Tests.Integration;

public class RateLimitingTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly CustomWebApplicationFactory _factory;

    public RateLimitingTests(CustomWebApplicationFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task LoginEndpoint_Returns429_AfterExceedingTheFixedWindowLimit()
    {
        var client = _factory.CreateClient();
        var request = new LoginRequest("rate-limit-test@clinicdesk.test", "NoExiste1!");

        HttpResponseMessage? lastResponse = null;
        for (var i = 0; i < 11; i++)
        {
            lastResponse = await client.PostAsJsonAsync("/api/auth/login", request);
        }

        Assert.Equal(HttpStatusCode.TooManyRequests, lastResponse!.StatusCode);
    }
}
