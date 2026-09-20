using EmployeeManagement.Application.Employees;
using EmployeeManagement.Domain.Employees;

namespace EmployeeManagement.UnitTests.Employees;

public class EmployeeMappingTests
{
    private static readonly DateTimeOffset CreatedAt = new(2025, 1, 10, 9, 0, 0, TimeSpan.Zero);

    private static EmployeeRequest DistinctRequest() => new(
        Name: "name-value",
        HireDate: "2023-04-17",
        Email: "email@value.com",
        PhoneNo: "+15550100001",
        ProfilePicture: "https://example.com/picture-value.jpg",
        Status: "inactive",
        Address: "address-value",
        State: "state-value",
        Country: "country-value",
        City: "city-value",
        Pincode: "pincode-1");

    private static Employee DistinctEmployee() => DistinctRequest().ToEmployee(CreatedAt);

    [Fact]
    public void ToEmployee_MapsEveryField_AndLetsTheSystemSetIdAndCreatedAt()
    {
        var employee = DistinctRequest().ToEmployee(CreatedAt);

        Assert.Equal("name-value", employee.Name);
        Assert.Equal(new DateOnly(2023, 4, 17), employee.HireDate);
        Assert.Equal("email@value.com", employee.Email);
        Assert.Equal("+15550100001", employee.PhoneNo);
        Assert.Equal("https://example.com/picture-value.jpg", employee.ProfilePictureUrl);
        Assert.Equal(EmployeeStatus.Inactive, employee.Status);
        Assert.Equal("address-value", employee.Address);
        Assert.Equal("state-value", employee.State);
        Assert.Equal("country-value", employee.Country);
        Assert.Equal("city-value", employee.City);
        Assert.Equal("pincode-1", employee.Pincode);
        Assert.NotEqual(Guid.Empty, employee.Id);
        Assert.Equal(CreatedAt, employee.CreatedAt);
    }

    [Fact]
    public void ToResponse_MapsEveryField_AndWritesStatusInLowercase()
    {
        var employee = DistinctEmployee();

        var response = employee.ToResponse();

        Assert.Equal(employee.Id, response.Id);
        Assert.Equal("name-value", response.Name);
        Assert.Equal(new DateOnly(2023, 4, 17), response.HireDate);
        Assert.Equal("email@value.com", response.Email);
        Assert.Equal("+15550100001", response.PhoneNo);
        Assert.Equal("https://example.com/picture-value.jpg", response.ProfilePicture);
        Assert.Equal("inactive", response.Status);
        Assert.Equal("address-value", response.Address);
        Assert.Equal("state-value", response.State);
        Assert.Equal("country-value", response.Country);
        Assert.Equal("city-value", response.City);
        Assert.Equal("pincode-1", response.Pincode);
        Assert.Equal(CreatedAt, response.CreatedAt);
    }

    [Fact]
    public void ApplyTo_ReplacesEveryEditableField_ButKeepsIdAndCreatedAt()
    {
        var employee = Employee.Create(
            "old-name", new DateOnly(2020, 1, 1), "old@example.com", "+15550100000", "https://example.com/old.jpg",
            EmployeeStatus.Active, "old-address", "old-state", "old-country", "old-city", "old-1", CreatedAt);
        var id = employee.Id;

        DistinctRequest().ApplyTo(employee);

        Assert.Equal("name-value", employee.Name);
        Assert.Equal(new DateOnly(2023, 4, 17), employee.HireDate);
        Assert.Equal("email@value.com", employee.Email);
        Assert.Equal("+15550100001", employee.PhoneNo);
        Assert.Equal("https://example.com/picture-value.jpg", employee.ProfilePictureUrl);
        Assert.Equal(EmployeeStatus.Inactive, employee.Status);
        Assert.Equal("address-value", employee.Address);
        Assert.Equal("state-value", employee.State);
        Assert.Equal("country-value", employee.Country);
        Assert.Equal("city-value", employee.City);
        Assert.Equal("pincode-1", employee.Pincode);
        Assert.Equal(id, employee.Id);
        Assert.Equal(CreatedAt, employee.CreatedAt);
    }

    [Fact]
    public void ProfilePicture_StaysOptional_InBothDirections()
    {
        var employee = (DistinctRequest() with { ProfilePicture = null }).ToEmployee(CreatedAt);

        Assert.Null(employee.ProfilePictureUrl);
        Assert.Null(employee.ToResponse().ProfilePicture);
    }
}
