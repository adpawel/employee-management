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

    [Fact]
    public void TextFields_AreTrimmed_ButKeepTheirCase()
    {
        var normalized = (EmployeeRequestValidatorTests.ValidRequest() with
        {
            Name = "  Jane Doe ",
            HireDate = " 2023-04-17 ",
            Status = " Active ",
            Address = " 1 Main Street ",
            State = " MA ",
            Country = " USA ",
            City = " Boston ",
            Pincode = " 02101 "
        }).Normalize();

        Assert.Equal("Jane Doe", normalized.Name);
        Assert.Equal("2023-04-17", normalized.HireDate);
        Assert.Equal("Active", normalized.Status);
        Assert.Equal("1 Main Street", normalized.Address);
        Assert.Equal("MA", normalized.State);
        Assert.Equal("USA", normalized.Country);
        Assert.Equal("Boston", normalized.City);
        Assert.Equal("02101", normalized.Pincode);
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

    [Fact]
    public void NullFields_StayNull()
    {
        var normalized = new EmployeeRequest(null, null, null, null, null, null, null, null, null, null, null).Normalize();

        Assert.Equal(new EmployeeRequest(null, null, null, null, null, null, null, null, null, null, null), normalized);
    }
}
