using System.Net;

namespace EmployeeManagement.IntegrationTests;

public class HealthEndpointTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    private readonly HttpClient _client = factory.CreateClient();

    [Fact]
    public async Task Get_Health_ReturnsOk()
    {
        var response = await _client.GetAsync("/health");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task Get_HealthDb_ReturnsOk_WhenDatabaseIsReachable()
    {
        var response = await _client.GetAsync("/health/db");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }
}
