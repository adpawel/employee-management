using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using EmployeeManagement.Application.Employees;

namespace EmployeeManagement.IntegrationTests;

public class EmployeesBulkImportTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    private const string Header = "Name,HireDate,Email,PhoneNo,ProfilePicture,Status,Address,State,Country,City,Pincode";

    private readonly HttpClient _client = factory.CreateClient();

    private static string SamplePath => Path.Combine(AppContext.BaseDirectory, "Data", "employees_sample.csv");

    private static string ValidLine(string email) =>
        $"Jane Doe,2023-04-17,{email},+1-555-0101,,active,1 Main Street,Massachusetts,USA,Boston,02101";

    private Task<HttpResponseMessage> UploadAsync(byte[] content, string fileName = "employees.csv")
    {
        var file = new ByteArrayContent(content);
        file.Headers.ContentType = new MediaTypeHeaderValue("text/csv");
        var form = new MultipartFormDataContent { { file, "file", fileName } };

        return _client.PostAsync("/employees/bulk", form);
    }

    private Task<HttpResponseMessage> UploadAsync(string csv) => UploadAsync(Encoding.UTF8.GetBytes(csv));

    private static async Task<EmployeeImportResult> ReadReportAsync(HttpResponseMessage response)
    {
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<EmployeeImportResult>())!;
    }

    private static async Task<JsonElement> ReadProblemErrorsAsync(HttpResponseMessage response)
    {
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType!.MediaType);
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        return json.RootElement.GetProperty("errors").Clone();
    }

    [Fact]
    public async Task SampleFile_ImportsValidRows_ReportsInvalidOnes_AndIsSafeToImportAgain()
    {
        var sample = await File.ReadAllBytesAsync(SamplePath);

        var first = await ReadReportAsync(await UploadAsync(sample, "employees_sample.csv"));

        Assert.Equal((10, 6, 4), (first.Total, first.Imported, first.Rejected));

        var rejected = first.Results.Where(r => r.Status == EmployeeImportRowResult.RejectedStatus).ToList();
        Assert.Equal([3, 8, 9, 11], rejected.Select(r => r.Row));
        Assert.All(rejected, r => Assert.Equal(["hireDate"], r.Errors!.Keys));

        var imported = first.Results.Where(r => r.Status == EmployeeImportRowResult.ImportedStatus).ToList();
        foreach (var row in imported)
        {
            var employee = await _client.GetFromJsonAsync<EmployeeResponse>($"/employee/{row.Id}");
            Assert.Equal(row.Email, employee!.Email);
        }

        var john = await _client.GetFromJsonAsync<PagedResult<EmployeeResponse>>("/employees?search=john.smith@company.com");
        var johnSmith = Assert.Single(john!.Items);
        Assert.Equal("+15550101", johnSmith.PhoneNo);

        Assert.True(johnSmith.CreatedAt > DateTimeOffset.UtcNow.AddMinutes(-5));

        var second = await ReadReportAsync(await UploadAsync(sample, "employees_sample.csv"));

        Assert.Equal((10, 0, 10), (second.Total, second.Imported, second.Rejected));
        Assert.All(second.Results.Where(r => r.Errors!.ContainsKey("email")),
            r => Assert.Contains("already exists", Assert.Single(r.Errors!["email"])));
        Assert.Equal(6, second.Results.Count(r => r.Errors!.ContainsKey("email")));
        Assert.Equal(4, second.Results.Count(r => r.Errors!.ContainsKey("hireDate")));
    }

    [Fact]
    public async Task DuplicateEmailInFile_ImportsFirstOccurrenceOnly()
    {
        var email = $"{Guid.NewGuid():N}@example.com";

        var report = await ReadReportAsync(await UploadAsync($"{Header}\n{ValidLine(email)}\n{ValidLine(email.ToUpperInvariant())}"));

        Assert.Equal((1, 1), (report.Imported, report.Rejected));
        Assert.Contains("row 2", Assert.Single(report.Results[1].Errors!["email"]));
    }

    [Fact]
    public async Task MissingFile_Returns400()
    {
        var response = await _client.PostAsync("/employees/bulk", new MultipartFormDataContent());

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType!.MediaType);
    }

    [Fact]
    public async Task MissingColumns_Returns400_WithFileError()
    {
        var errors = await ReadProblemErrorsAsync(await UploadAsync("Name,Email\nJane,jane@example.com"));

        Assert.Contains("HireDate", errors.GetProperty("file")[0].GetString());
    }

    [Fact]
    public async Task BinaryContentWithCsvExtension_Returns400()
    {
        var errors = await ReadProblemErrorsAsync(await UploadAsync([0x89, 0x50, 0x4E, 0x47, 0xFF, 0xFE, 0x00, 0xC3, 0x28]));

        Assert.True(errors.TryGetProperty("file", out _));
    }

    [Fact]
    public async Task FileOver1MB_Returns413()
    {
        var response = await UploadAsync(new byte[1_048_577]);

        Assert.Equal(HttpStatusCode.RequestEntityTooLarge, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType!.MediaType);
    }
}
