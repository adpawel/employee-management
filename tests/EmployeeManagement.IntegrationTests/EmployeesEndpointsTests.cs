using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using EmployeeManagement.Application.Employees;

namespace EmployeeManagement.IntegrationTests;

// The database is shared between tests, so each test uses unique emails instead of cleaning up.
public class EmployeesEndpointsTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    private readonly HttpClient _client = factory.CreateClient();

    private static string UniqueToken() => Guid.NewGuid().ToString("N");

    private static object ValidBody(string email, string? name = null) => new
    {
        name = name ?? "Jane Doe",
        hireDate = "2023-04-17",
        email,
        phoneNo = "+1 (555) 010-1234",
        profilePicture = "https://example.com/jane.jpg",
        status = "Active",
        address = "1 Main Street",
        state = "Massachusetts",
        country = "USA",
        city = "Boston",
        pincode = "02101"
    };

    private async Task<(HttpResponseMessage Response, EmployeeResponse Employee)> CreateAsync(string email, string? name = null)
    {
        var response = await _client.PostAsJsonAsync("/employee", ValidBody(email, name));
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var employee = await response.Content.ReadFromJsonAsync<EmployeeResponse>();
        return (response, employee!);
    }

    [Fact]
    public async Task Post_ValidEmployee_Returns201_WithLocation_AndNormalizedData()
    {
        var email = $"Jane.{UniqueToken()}@Example.com";

        var (response, created) = await CreateAsync(email);

        Assert.NotEqual(Guid.Empty, created.Id);
        Assert.Equal(email.ToLowerInvariant(), created.Email);
        Assert.Equal("+15550101234", created.PhoneNo);
        Assert.Equal("active", created.Status);
        Assert.Equal(new DateOnly(2023, 4, 17), created.HireDate);
        Assert.True(created.CreatedAt > DateTimeOffset.UtcNow.AddMinutes(-5));

        Assert.EndsWith($"/employee/{created.Id}", response.Headers.Location!.ToString());
        var fetched = await _client.GetAsync(response.Headers.Location);
        Assert.Equal(HttpStatusCode.OK, fetched.StatusCode);
        Assert.Equal(created, await fetched.Content.ReadFromJsonAsync<EmployeeResponse>());
    }

    [Fact]
    public async Task Post_IgnoresClientSuppliedIdAndCreatedAt()
    {
        var fakeId = Guid.NewGuid();
        var body = new
        {
            id = fakeId,
            createdAt = "1999-01-01T00:00:00Z",
            name = "Jane Doe",
            hireDate = "2023-04-17",
            email = $"{UniqueToken()}@example.com",
            phoneNo = "+15550101234",
            status = "active",
            address = "1 Main Street",
            state = "MA",
            country = "USA",
            city = "Boston",
            pincode = "02101"
        };

        var response = await _client.PostAsJsonAsync("/employee", body);
        var created = await response.Content.ReadFromJsonAsync<EmployeeResponse>();

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.NotEqual(fakeId, created!.Id);
        Assert.True(created.CreatedAt.Year >= 2025);
    }

    [Fact]
    public async Task Post_InvalidData_Returns400_WithPerFieldErrors()
    {
        var body = new
        {
            name = "  ",
            hireDate = "2999-01-01",
            email = "not-an-email",
            phoneNo = "12345",
            profilePicture = "javascript:alert(1)",
            status = "unknown",
            address = "1 Main Street",
            state = "MA",
            country = "USA",
            city = "Boston",
            pincode = "02101"
        };

        var response = await _client.PostAsJsonAsync("/employee", body);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType!.MediaType);

        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal(400, json.RootElement.GetProperty("status").GetInt32());
        var errors = json.RootElement.GetProperty("errors");
        foreach (var field in new[] { "name", "hireDate", "email", "phoneNo", "profilePicture", "status" })
        {
            Assert.True(errors.TryGetProperty(field, out _), $"Expected an error for '{field}'.");
        }
    }

    [Fact]
    public async Task Post_MalformedJson_Returns400()
    {
        var response = await _client.PostAsync("/employee",
            new StringContent("{ not json", System.Text.Encoding.UTF8, "application/json"));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Post_DuplicateEmail_Returns409_EvenWhenCaseDiffers()
    {
        var token = UniqueToken();
        await CreateAsync($"dup.{token}@example.com");

        var response = await _client.PostAsJsonAsync("/employee", ValidBody($"DUP.{token}@Example.com"));

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType!.MediaType);
    }

    [Fact]
    public async Task Post_ConcurrentRequestsWithSameEmail_CreateOne_AndRestGet409_Never500()
    {
        var body = ValidBody($"race.{UniqueToken()}@example.com");

        var responses = await Task.WhenAll(
            Enumerable.Range(0, 8).Select(_ => _client.PostAsJsonAsync("/employee", body)));

        // Losers of the race hit the unique index; that must surface as 409, not 500.
        Assert.Single(responses, r => r.StatusCode == HttpStatusCode.Created);
        Assert.All(responses.Where(r => r.StatusCode != HttpStatusCode.Created),
            r => Assert.Equal(HttpStatusCode.Conflict, r.StatusCode));
    }

    [Fact]
    public async Task Get_UnknownId_Returns404()
    {
        var response = await _client.GetAsync($"/employee/{Guid.NewGuid()}");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType!.MediaType);
    }

    [Fact]
    public async Task Put_ExistingEmployee_Returns200_WithUpdatedData_AndKeepsIdAndCreatedAt()
    {
        var (_, created) = await CreateAsync($"{UniqueToken()}@example.com");
        var newEmail = $"updated.{UniqueToken()}@example.com";

        var response = await _client.PutAsJsonAsync($"/employee/{created.Id}", ValidBody(newEmail, "Jane Doe-Smith"));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var updated = await response.Content.ReadFromJsonAsync<EmployeeResponse>();
        Assert.Equal(created.Id, updated!.Id);
        Assert.Equal(created.CreatedAt, updated.CreatedAt);
        Assert.Equal("Jane Doe-Smith", updated.Name);
        Assert.Equal(newEmail, updated.Email);

        var fetched = await _client.GetFromJsonAsync<EmployeeResponse>($"/employee/{created.Id}");
        Assert.Equal(updated, fetched);
    }

    [Fact]
    public async Task Put_EmailOfAnotherEmployee_Returns409()
    {
        var takenEmail = $"{UniqueToken()}@example.com";
        await CreateAsync(takenEmail);
        var (_, other) = await CreateAsync($"{UniqueToken()}@example.com");

        var response = await _client.PutAsJsonAsync($"/employee/{other.Id}", ValidBody(takenEmail));

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Fact]
    public async Task Put_InvalidData_Returns400()
    {
        var (_, created) = await CreateAsync($"{UniqueToken()}@example.com");

        var response = await _client.PutAsJsonAsync($"/employee/{created.Id}",
            new { name = "Jane", hireDate = "2019-13-12" });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Put_UnknownId_Returns404()
    {
        var response = await _client.PutAsJsonAsync($"/employee/{Guid.NewGuid()}", ValidBody($"{UniqueToken()}@example.com"));

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Delete_ExistingEmployee_Returns204_ThenGetReturns404()
    {
        var (_, created) = await CreateAsync($"{UniqueToken()}@example.com");

        var delete = await _client.DeleteAsync($"/employee/{created.Id}");
        var get = await _client.GetAsync($"/employee/{created.Id}");
        var deleteAgain = await _client.DeleteAsync($"/employee/{created.Id}");

        Assert.Equal(HttpStatusCode.NoContent, delete.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, get.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, deleteAgain.StatusCode);
    }

    [Fact]
    public async Task Get_Employees_SupportsSearchAndPaging()
    {
        var token = UniqueToken();
        foreach (var letter in new[] { "A", "B", "C" })
        {
            await CreateAsync($"{letter}.{token}@example.com", name: $"{letter} {token}");
        }

        var firstPage = await _client.GetFromJsonAsync<PagedResult<EmployeeResponse>>(
            $"/employees?search={token}&page=1&pageSize=2");
        var secondPage = await _client.GetFromJsonAsync<PagedResult<EmployeeResponse>>(
            $"/employees?search={token}&page=2&pageSize=2");

        Assert.Equal(3, firstPage!.TotalCount);
        Assert.Equal(["A " + token, "B " + token], firstPage.Items.Select(e => e.Name));
        Assert.Equal(1, firstPage.Page);
        Assert.Equal(2, firstPage.PageSize);
        Assert.Equal(["C " + token], secondPage!.Items.Select(e => e.Name));
    }

    [Fact]
    public async Task Get_Employees_Search_IsCaseInsensitive()
    {
        var token = UniqueToken();
        await CreateAsync($"{token}@example.com", name: $"Jane {token}");

        var result = await _client.GetFromJsonAsync<PagedResult<EmployeeResponse>>(
            $"/employees?search=JANE%20{token.ToUpperInvariant()}");

        Assert.Equal(1, result!.TotalCount);
    }

    // Each pattern would match "ab{token}" if the character were interpreted as a LIKE wildcard.
    [Theory]
    [InlineData("a%")]
    [InlineData("a_")]
    [InlineData("a[b]")]
    public async Task Get_Employees_SearchWithWildcardCharacters_IsTreatedLiterally(string prefix)
    {
        var token = UniqueToken();
        await CreateAsync($"{token}@example.com", name: $"ab{token}");

        var result = await _client.GetFromJsonAsync<PagedResult<EmployeeResponse>>(
            $"/employees?search={Uri.EscapeDataString(prefix + token)}");

        Assert.Equal(0, result!.TotalCount);
    }

    [Theory]
    [InlineData("page=0")]
    [InlineData("pageSize=0")]
    [InlineData("pageSize=101")]
    [InlineData("page=abc")]
    public async Task Get_Employees_InvalidPaging_Returns400(string query)
    {
        var response = await _client.GetAsync($"/employees?{query}");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }
}
