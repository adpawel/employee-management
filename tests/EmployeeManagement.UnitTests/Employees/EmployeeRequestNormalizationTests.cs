using EmployeeManagement.Application.Employees;

namespace EmployeeManagement.UnitTests.Employees;

public class EmployeeRequestNormalizationTests
{
    [Theory]
    [InlineData("+1-555-0101", "+15550101")]
    [InlineData("+1 (555) 010-1234", "+15550101234")]
    [InlineData("  +48 123 456 789 ", "+48123456789")]
    public void PhoneNo_SeparatorsAreStripped(string input, string expected)
    {
        var normalized = (EmployeeRequestValidatorTests.ValidRequest() with { PhoneNo = input }).Normalize();

        Assert.Equal(expected, normalized.PhoneNo);
    }

    [Fact]
    public void PhoneNo_LettersAreNotSilentlyRemoved()
    {
        var normalized = (EmployeeRequestValidatorTests.ValidRequest() with { PhoneNo = "+1-555-CALL" }).Normalize();

        Assert.Equal("+1555CALL", normalized.PhoneNo);
    }

    [Fact]
    public void Email_IsTrimmedAndLowercased()
    {
        var normalized = (EmployeeRequestValidatorTests.ValidRequest() with { Email = "  Jane.DOE@Example.COM " }).Normalize();

        Assert.Equal("jane.doe@example.com", normalized.Email);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void BlankProfilePicture_BecomesNull(string? input)
    {
        var normalized = (EmployeeRequestValidatorTests.ValidRequest() with { ProfilePicture = input }).Normalize();

        Assert.Null(normalized.ProfilePicture);
    }
}
