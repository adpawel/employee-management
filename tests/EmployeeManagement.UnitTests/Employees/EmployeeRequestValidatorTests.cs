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

    [Fact]
    public void ProfilePicture_IsOptional()
    {
        _validator.TestValidate(ValidRequest() with { ProfilePicture = null })
            .ShouldNotHaveValidationErrorFor(x => x.ProfilePicture);
    }

    [Theory]
    [InlineData("2025-06-15")]
    [InlineData("2025-06-14")]
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
    [InlineData("2023-4-5")]
    [InlineData("17/04/2023")]
    [InlineData("04-17-2023")]
    [InlineData("2023-04-17T10:00:00")]
    [InlineData("not a date")]
    public void HireDate_WithWrongFormatOrNonexistentDate_IsRejected(string hireDate)
    {
        _validator.TestValidate(ValidRequest() with { HireDate = hireDate })
            .ShouldHaveValidationErrorFor(x => x.HireDate)
            .WithErrorMessage("'Hire Date' must be a valid date in yyyy-MM-dd format.");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void HireDate_Missing_IsRejected(string? hireDate)
    {
        _validator.TestValidate(ValidRequest() with { HireDate = hireDate })
            .ShouldHaveValidationErrorFor(x => x.HireDate);
    }

    [Theory]
    [InlineData("john@example.com")]
    [InlineData("first.last+tag@sub.example.co.uk")]
    public void Email_Accepts(string email)
    {
        _validator.TestValidate(ValidRequest() with { Email = email })
            .ShouldNotHaveValidationErrorFor(x => x.Email);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("not-an-email")]
    [InlineData("@example.com")]
    [InlineData("john@")]
    public void Email_MissingOrMalformed_IsRejected(string? email)
    {
        _validator.TestValidate(ValidRequest() with { Email = email })
            .ShouldHaveValidationErrorFor(x => x.Email);
    }

    [Fact]
    public void Email_LongerThan254Characters_IsRejected()
    {
        var email = new string('a', 250) + "@b.co";

        _validator.TestValidate(ValidRequest() with { Email = email })
            .ShouldHaveValidationErrorFor(x => x.Email);
    }

    [Theory]
    [InlineData("+15550101234")]
    [InlineData("+48123456789")]
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
    [InlineData("+1555abc1234")]
    [InlineData("+1555010123\n")]
    [InlineData("+١٢٣٤٥٦٧٨٩")] // Arabic-Indic digits are not ASCII digits
    public void PhoneNo_Invalid_IsRejected(string? phone)
    {
        _validator.TestValidate(ValidRequest() with { PhoneNo = phone })
            .ShouldHaveValidationErrorFor(x => x.PhoneNo);
    }

    [Theory]
    [InlineData("active")]
    [InlineData("Active")]
    [InlineData("INACTIVE")]
    [InlineData("inactive")]
    public void Status_Accepts(string status)
    {
        _validator.TestValidate(ValidRequest() with { Status = status })
            .ShouldNotHaveValidationErrorFor(x => x.Status);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("unknown")]
    [InlineData("1")] // valid enum number, still rejected
    [InlineData("active,inactive")]
    [InlineData("activ")]
    public void Status_MissingOrUnknown_IsRejected(string? status)
    {
        _validator.TestValidate(ValidRequest() with { Status = status })
            .ShouldHaveValidationErrorFor(x => x.Status);
    }

    [Theory]
    [InlineData("http://example.com/a.png")]
    [InlineData("https://cdn.example.com/a/b.jpg?size=200")]
    public void ProfilePicture_HttpUrls_AreAccepted(string url)
    {
        _validator.TestValidate(ValidRequest() with { ProfilePicture = url })
            .ShouldNotHaveValidationErrorFor(x => x.ProfilePicture);
    }

    [Theory]
    [InlineData("javascript:alert(1)")]
    [InlineData("data:image/png;base64,AAAA")]
    [InlineData("ftp://example.com/a.png")]
    [InlineData("file:///etc/passwd")]
    [InlineData("/relative/path.png")]
    [InlineData("example.com/a.png")]
    public void ProfilePicture_NonHttpOrRelative_IsRejected(string url)
    {
        _validator.TestValidate(ValidRequest() with { ProfilePicture = url })
            .ShouldHaveValidationErrorFor(x => x.ProfilePicture);
    }

    [Fact]
    public void ProfilePicture_LongerThan2048Characters_IsRejected()
    {
        var url = "https://example.com/" + new string('a', 2049);

        _validator.TestValidate(ValidRequest() with { ProfilePicture = url })
            .ShouldHaveValidationErrorFor(x => x.ProfilePicture);
    }

    [Theory]
    [InlineData("02101")]
    [InlineData("SW1A 1AA")]
    [InlineData("00-950")]
    [InlineData("123")]
    [InlineData("1234567890")]
    public void Pincode_Accepts(string pincode)
    {
        _validator.TestValidate(ValidRequest() with { Pincode = pincode })
            .ShouldNotHaveValidationErrorFor(x => x.Pincode);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("12")]
    [InlineData("12345678901")]
    [InlineData("12<3>")]
    [InlineData("12345;DROP")]
    public void Pincode_Invalid_IsRejected(string? pincode)
    {
        _validator.TestValidate(ValidRequest() with { Pincode = pincode })
            .ShouldHaveValidationErrorFor(x => x.Pincode);
    }

    [Theory]
    [InlineData("Jane Doe")]
    [InlineData("Patrick O'Brien")]
    [InlineData("Anna Kowalska-Nowak")]
    [InlineData("Żółtowski Ślązak")]
    [InlineData("李小龙")]
    public void Name_AnyAlphabet_IsAccepted(string name)
    {
        _validator.TestValidate(ValidRequest() with { Name = name })
            .ShouldNotHaveValidationErrorFor(x => x.Name);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("Jane\u0000Doe")]
    [InlineData("Jane\nDoe")]
    public void Name_MissingBlankOrWithControlCharacters_IsRejected(string? name)
    {
        _validator.TestValidate(ValidRequest() with { Name = name })
            .ShouldHaveValidationErrorFor(x => x.Name);
    }

    [Fact]
    public void Name_LongerThan200Characters_IsRejected()
    {
        _validator.TestValidate(ValidRequest() with { Name = new string('a', 201) })
            .ShouldHaveValidationErrorFor(x => x.Name);
    }

    [Fact]
    public void RequiredTextFields_AreRejectedWhenBlank()
    {
        var result = _validator.TestValidate(ValidRequest() with
        {
            Address = " ",
            State = "",
            Country = null,
            City = "\t"
        });

        result.ShouldHaveValidationErrorFor(x => x.Address);
        result.ShouldHaveValidationErrorFor(x => x.State);
        result.ShouldHaveValidationErrorFor(x => x.Country);
        result.ShouldHaveValidationErrorFor(x => x.City);
    }

    [Fact]
    public void TextFields_LongerThanTheirLimit_AreRejected()
    {
        var result = _validator.TestValidate(ValidRequest() with
        {
            Address = new string('a', 501),
            State = new string('a', 101),
            Country = new string('a', 101),
            City = new string('a', 101)
        });

        result.ShouldHaveValidationErrorFor(x => x.Address);
        result.ShouldHaveValidationErrorFor(x => x.State);
        result.ShouldHaveValidationErrorFor(x => x.Country);
        result.ShouldHaveValidationErrorFor(x => x.City);
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
