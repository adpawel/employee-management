using EmployeeManagement.Application.Employees;
using FluentValidation.TestHelper;
using Microsoft.Extensions.Time.Testing;

namespace EmployeeManagement.UnitTests.Employees;

public class EmployeeRequestValidatorTests
{
    private static readonly DateTimeOffset Now = new(2025, 6, 15, 12, 0, 0, TimeSpan.Zero);

    private readonly EmployeeRequestValidator _validator = new(new FakeTimeProvider(Now));

    internal static EmployeeRequest ValidRequest() => new(
        Name: "Jane Doe",
        HireDate: "2023-04-17",
        Email: "jane.doe@example.com",
        PhoneNo: "+15550101234",
        ProfilePicture: "https://example.com/jane.jpg",
        Status: "active",
        Address: "1 Main Street",
        State: "Massachusetts",
        Country: "USA",
        City: "Boston",
        Pincode: "02101");

    [Fact]
    public void ValidRequest_HasNoErrors()
    {
        _validator.TestValidate(ValidRequest()).ShouldNotHaveAnyValidationErrors();
    }

    [Theory]
    [InlineData("2025-06-15")]
    [InlineData("1900-01-01")] // the lower bound is inclusive
    public void HireDate_Accepts(string hireDate)
    {
        _validator.TestValidate(ValidRequest() with { HireDate = hireDate })
            .ShouldNotHaveValidationErrorFor(x => x.HireDate);
    }

    [Theory]
    [InlineData("2025-06-16")]
    [InlineData("2079-01-01")]
    public void HireDate_InTheFuture_IsRejected(string hireDate)
    {
        _validator.TestValidate(ValidRequest() with { HireDate = hireDate })
            .ShouldHaveValidationErrorFor(x => x.HireDate)
            .WithErrorMessage("'Hire Date' must not be in the future.");
    }

    [Fact]
    public void HireDate_IsComparedInUtc_NotInLocalTime()
    {
        // 23:30 UTC on the 15th is already the 16th in UTC+2, but "today" is decided in UTC.
        var lateEvening = new FakeTimeProvider(new DateTimeOffset(2025, 6, 15, 23, 30, 0, TimeSpan.Zero));
        lateEvening.SetLocalTimeZone(TimeZoneInfo.CreateCustomTimeZone("plus2", TimeSpan.FromHours(2), "plus2", "plus2"));
        var validator = new EmployeeRequestValidator(lateEvening);

        validator.TestValidate(ValidRequest() with { HireDate = "2025-06-15" })
            .ShouldNotHaveValidationErrorFor(x => x.HireDate);
        validator.TestValidate(ValidRequest() with { HireDate = "2025-06-16" })
            .ShouldHaveValidationErrorFor(x => x.HireDate);
    }

    [Theory]
    [InlineData("1899-12-31")]
    [InlineData("1899-02-28")] // Amanda Thomas in the sample CSV
    public void HireDate_BeforeMinimum_IsRejected(string hireDate)
    {
        _validator.TestValidate(ValidRequest() with { HireDate = hireDate })
            .ShouldHaveValidationErrorFor(x => x.HireDate)
            .WithErrorMessage("'Hire Date' must not be earlier than 1900-01-01.");
    }

    [Theory]
    [InlineData("2019-13-12")] // David Anderson in the sample CSV
    [InlineData("2023-02-30")]
    [InlineData("17/04/2023")]
    [InlineData("2023-04-17T10:00:00")]
    public void HireDate_WithWrongFormatOrNonexistentDate_IsRejected(string hireDate)
    {
        _validator.TestValidate(ValidRequest() with { HireDate = hireDate })
            .ShouldHaveValidationErrorFor(x => x.HireDate)
            .WithErrorMessage("'Hire Date' must be a valid date in yyyy-MM-dd format.");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("not-an-email")]
    public void Email_MissingOrMalformed_IsRejected(string? email)
    {
        _validator.TestValidate(ValidRequest() with { Email = email })
            .ShouldHaveValidationErrorFor(x => x.Email);
    }

    [Theory]
    [InlineData("+15550101234")]
    [InlineData("+12345678")] // 8 digits: shortest accepted
    [InlineData("+123456789012345")] // 15 digits: longest accepted (E.164)
    public void PhoneNo_Accepts(string phone)
    {
        _validator.TestValidate(ValidRequest() with { PhoneNo = phone })
            .ShouldNotHaveValidationErrorFor(x => x.PhoneNo);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("15550101234")]
    [InlineData("+0555010123")] // country code cannot start with 0
    [InlineData("+1234567")]
    [InlineData("+1234567890123456")]
    [InlineData("+1555010123\n")]
    [InlineData("+١٢٣٤٥٦٧٨٩")] // Arabic-Indic digits are not ASCII digits
    public void PhoneNo_Invalid_IsRejected(string? phone)
    {
        _validator.TestValidate(ValidRequest() with { PhoneNo = phone })
            .ShouldHaveValidationErrorFor(x => x.PhoneNo);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("unknown")]
    [InlineData("1")] // valid enum number, still rejected
    public void Status_MissingOrUnknown_IsRejected(string? status)
    {
        _validator.TestValidate(ValidRequest() with { Status = status })
            .ShouldHaveValidationErrorFor(x => x.Status);
    }

    [Theory]
    [InlineData("javascript:alert(1)")]
    [InlineData("data:image/png;base64,AAAA")]
    [InlineData("file:///etc/passwd")]
    [InlineData("/relative/path.png")]
    public void ProfilePicture_NonHttpOrRelative_IsRejected(string url)
    {
        _validator.TestValidate(ValidRequest() with { ProfilePicture = url })
            .ShouldHaveValidationErrorFor(x => x.ProfilePicture);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("12")]
    [InlineData("12<3>")]
    [InlineData("12345;DROP")]
    public void Pincode_Invalid_IsRejected(string? pincode)
    {
        _validator.TestValidate(ValidRequest() with { Pincode = pincode })
            .ShouldHaveValidationErrorFor(x => x.Pincode);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("Jane\u0000Doe")]
    public void Name_MissingBlankOrWithControlCharacters_IsRejected(string? name)
    {
        _validator.TestValidate(ValidRequest() with { Name = name })
            .ShouldHaveValidationErrorFor(x => x.Name);
    }

    [Fact]
    public void EmptyRequest_ReportsEveryRequiredField()
    {
        var result = _validator.TestValidate(new EmployeeRequest(null, null, null, null, null, null, null, null, null, null, null));

        result.ShouldHaveValidationErrorFor(x => x.Name);
        result.ShouldHaveValidationErrorFor(x => x.HireDate);
        result.ShouldHaveValidationErrorFor(x => x.Email);
        result.ShouldHaveValidationErrorFor(x => x.PhoneNo);
        result.ShouldHaveValidationErrorFor(x => x.Status);
        result.ShouldHaveValidationErrorFor(x => x.Address);
        result.ShouldHaveValidationErrorFor(x => x.State);
        result.ShouldHaveValidationErrorFor(x => x.Country);
        result.ShouldHaveValidationErrorFor(x => x.City);
        result.ShouldHaveValidationErrorFor(x => x.Pincode);
        result.ShouldNotHaveValidationErrorFor(x => x.ProfilePicture);
    }
}
