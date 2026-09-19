using System.Text;
using EmployeeManagement.Application.Employees;
using EmployeeManagement.Infrastructure.Csv;
using FluentValidation;

namespace EmployeeManagement.UnitTests.Employees;

public class EmployeeCsvReaderTests
{
    private const string Header = "Name,HireDate,Email,PhoneNo,ProfilePicture,Status,Address,State,Country,City,Pincode";

    private const string ValidLine =
        "John Smith,2022-03-15,john.smith@company.com,+1-555-0101,https://example.com/john.jpg,active,"
        + "123 Oak Street,California,USA,Los Angeles,90001";

    private readonly EmployeeCsvReader _reader = new();

    private Task<IReadOnlyList<EmployeeImportRow>> ReadAsync(byte[] bytes) =>
        _reader.ReadAsync(new MemoryStream(bytes), CancellationToken.None);

    private Task<IReadOnlyList<EmployeeImportRow>> ReadAsync(string csv) => ReadAsync(Encoding.UTF8.GetBytes(csv));

    private async Task<string> ReadFileErrorAsync(string csv)
    {
        var ex = await Assert.ThrowsAsync<ValidationException>(() => ReadAsync(csv));
        var error = Assert.Single(ex.Errors);
        Assert.Equal(EmployeeImportFileError.Key, error.PropertyName);
        return error.ErrorMessage;
    }

    [Fact]
    public async Task ValidFile_MapsColumnsToRequest_WithoutConvertingValues()
    {
        var rows = await ReadAsync($"{Header}\n{ValidLine}\n");

        var row = Assert.Single(rows);
        Assert.Equal(2, row.Row);
        Assert.Equal(new EmployeeRequest(
            "John Smith", "2022-03-15", "john.smith@company.com", "+1-555-0101", "https://example.com/john.jpg",
            "active", "123 Oak Street", "California", "USA", "Los Angeles", "90001"), row.Request);
    }

    [Fact]
    public async Task QuotedFieldWithCommaAndQuotes_IsReadAsOneValue()
    {
        var rows = await ReadAsync(
            $"{Header}\n\"Smith, John \"\"Johnny\"\"\",2022-03-15,j@x.com,+15550101,,active,\"1 Main St, Apt 2\",CA,USA,LA,90001");

        var request = Assert.Single(rows).Request;
        Assert.Equal("Smith, John \"Johnny\"", request.Name);
        Assert.Equal("1 Main St, Apt 2", request.Address);
    }

    [Fact]
    public async Task HeaderCase_AndExtraColumns_AreIgnored()
    {
        // Mirrors data/employees_sample.csv: "Hiredate" and a CreatedAt column that the system sets itself.
        var rows = await ReadAsync(
            "name,Hiredate,EMAIL,PhoneNo,ProfilePicture,Status,Address,State,Country,City,Pincode,CreatedAt\n"
            + ValidLine + ",1999-01-01T00:00:00Z");

        var request = Assert.Single(rows).Request;
        Assert.Equal("John Smith", request.Name);
        Assert.Equal("2022-03-15", request.HireDate);
    }

    [Fact]
    public async Task Utf8Bom_IsNotPartOfTheFirstHeader()
    {
        byte[] bytes = [.. Encoding.UTF8.GetPreamble(), .. Encoding.UTF8.GetBytes($"{Header}\n{ValidLine}")];

        var rows = await ReadAsync(bytes);

        Assert.Equal("John Smith", Assert.Single(rows).Request.Name);
    }

    [Fact]
    public async Task MissingColumns_AreAFileError_ListingTheColumns()
    {
        var message = await ReadFileErrorAsync(
            "Name,HireDate,PhoneNo,ProfilePicture,Status,Address,State,Country,City\nJohn,2022-03-15,+15550101,,active,A,S,C,C");

        Assert.Contains("Email", message);
        Assert.Contains("Pincode", message);
    }

    [Fact]
    public async Task UnterminatedQuote_IsAFileError()
    {
        await ReadFileErrorAsync($"{Header}\n\"John Smith,2022-03-15,j@x.com,+15550101,,active,A,S,C,C,90001\n{ValidLine}");
    }

    [Fact]
    public async Task QuoteInsideUnquotedField_IsAFileError_WithLineNumber()
    {
        var message = await ReadFileErrorAsync(
            $"{Header}\n{ValidLine}\nJohn \"Johnny\" Smith,2022-03-15,j@x.com,+15550101,,active,A,S,C,C,90001");

        Assert.Contains("line 3", message);
    }
}
