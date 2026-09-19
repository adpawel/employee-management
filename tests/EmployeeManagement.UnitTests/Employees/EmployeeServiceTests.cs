using EmployeeManagement.Application.Common.Exceptions;
using EmployeeManagement.Application.Employees;
using EmployeeManagement.Domain.Employees;
using FluentValidation;
using Microsoft.Extensions.Time.Testing;

namespace EmployeeManagement.UnitTests.Employees;

public class EmployeeServiceTests
{
    private static readonly DateTimeOffset CreatedAt = new(2025, 1, 10, 9, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset Now = new(2025, 6, 15, 12, 0, 0, TimeSpan.Zero);

    private readonly FakeEmployeeRepository _repository = new();
    private readonly EmployeeService _service;

    public EmployeeServiceTests()
    {
        var timeProvider = new FakeTimeProvider(Now);
        _service = new EmployeeService(
            _repository,
            new EmployeeRequestValidator(timeProvider),
            new EmployeeListQueryValidator(),
            timeProvider);
    }

    private static EmployeeRequest Request(string email = "jane.doe@example.com") =>
        EmployeeRequestValidatorTests.ValidRequest() with { Email = email };

    private Employee Seed(string email)
    {
        var employee = Employee.Create(
            "Seeded Person", new DateOnly(2020, 1, 1), email, "+15550100000", null, EmployeeStatus.Active,
            "Somewhere 1", "State", "Country", "City", "12345", CreatedAt);
        _repository.Add(employee);
        return employee;
    }

    [Fact]
    public async Task Update_UnknownId_ThrowsNotFound()
    {
        await Assert.ThrowsAsync<NotFoundException>(() =>
            _service.UpdateAsync(Guid.NewGuid(), Request(), CancellationToken.None));
    }

    [Fact]
    public async Task Update_UnknownId_IsReportedBeforeInvalidBody()
    {
        var invalid = Request() with { Email = "not-an-email" };

        await Assert.ThrowsAsync<NotFoundException>(() =>
            _service.UpdateAsync(Guid.NewGuid(), invalid, CancellationToken.None));
    }

    [Fact]
    public async Task Update_EmailOfAnotherEmployee_ThrowsConflict()
    {
        Seed("taken@example.com");
        var employee = Seed("mine@example.com");

        await Assert.ThrowsAsync<ConflictException>(() =>
            _service.UpdateAsync(employee.Id, Request("taken@example.com"), CancellationToken.None));

        Assert.Equal("mine@example.com", employee.Email);
        Assert.Equal(0, _repository.SaveCount);
    }

    [Fact]
    public async Task Update_EmailOfAnotherEmployee_IsCaseInsensitive()
    {
        Seed("taken@example.com");
        var employee = Seed("mine@example.com");

        await Assert.ThrowsAsync<ConflictException>(() =>
            _service.UpdateAsync(employee.Id, Request("TAKEN@Example.com"), CancellationToken.None));
    }

    [Fact]
    public async Task Update_KeepingOwnEmail_Succeeds()
    {
        var employee = Seed("mine@example.com");

        var response = await _service.UpdateAsync(employee.Id, Request("mine@example.com"), CancellationToken.None);

        Assert.Equal("mine@example.com", response.Email);
        Assert.Equal(1, _repository.SaveCount);
    }

    [Fact]
    public async Task Update_DoesNotChangeIdOrCreatedAt()
    {
        var employee = Seed("mine@example.com");
        var id = employee.Id;

        var response = await _service.UpdateAsync(employee.Id, Request("mine@example.com"), CancellationToken.None);

        Assert.Equal(id, response.Id);
        Assert.Equal(CreatedAt, response.CreatedAt);
        Assert.Equal(id, employee.Id);
        Assert.Equal(CreatedAt, employee.CreatedAt);
    }

    [Fact]
    public async Task Update_ReplacesAllEditableFields_WithNormalizedValues()
    {
        var employee = Seed("mine@example.com");
        var request = new EmployeeRequest(
            Name: "  Jane Doe-Smith ",
            HireDate: "2023-04-17",
            Email: "  Jane.Doe@Example.com ",
            PhoneNo: "+1 (555) 010-1234",
            ProfilePicture: null,
            Status: "Inactive",
            Address: " 1 Main Street ",
            State: "MA",
            Country: "USA",
            City: "Boston",
            Pincode: "02101");

        var response = await _service.UpdateAsync(employee.Id, request, CancellationToken.None);

        Assert.Equal("Jane Doe-Smith", response.Name);
        Assert.Equal(new DateOnly(2023, 4, 17), response.HireDate);
        Assert.Equal("jane.doe@example.com", response.Email);
        Assert.Equal("+15550101234", response.PhoneNo);
        Assert.Null(response.ProfilePicture);
        Assert.Equal("inactive", response.Status);
        Assert.Equal("1 Main Street", response.Address);
        Assert.Equal("02101", response.Pincode);
        Assert.Equal(EmployeeStatus.Inactive, employee.Status);
    }

    [Fact]
    public async Task Update_InvalidRequest_ThrowsValidationAndChangesNothing()
    {
        var employee = Seed("mine@example.com");

        var ex = await Assert.ThrowsAsync<ValidationException>(() =>
            _service.UpdateAsync(employee.Id, Request("mine@example.com") with { Status = "unknown" }, CancellationToken.None));

        Assert.Contains(ex.Errors, e => e.PropertyName == nameof(EmployeeRequest.Status));
        Assert.Equal("Seeded Person", employee.Name);
        Assert.Equal(0, _repository.SaveCount);
    }

    [Fact]
    public async Task Create_SetsIdAndCreatedAtItself()
    {
        var response = await _service.CreateAsync(Request(), CancellationToken.None);

        Assert.NotEqual(Guid.Empty, response.Id);
        Assert.Equal(Now, response.CreatedAt);
        Assert.Equal("active", response.Status);
    }

    [Fact]
    public async Task Create_DuplicateEmail_ThrowsConflict()
    {
        Seed("jane.doe@example.com");

        await Assert.ThrowsAsync<ConflictException>(() =>
            _service.CreateAsync(Request(), CancellationToken.None));
    }

    [Fact]
    public async Task Create_InvalidRequest_ThrowsValidation_AndDoesNotSave()
    {
        await Assert.ThrowsAsync<ValidationException>(() =>
            _service.CreateAsync(Request() with { HireDate = "2999-01-01" }, CancellationToken.None));

        Assert.Equal(0, _repository.SaveCount);
    }

    [Fact]
    public async Task Get_UnknownId_ThrowsNotFound()
    {
        await Assert.ThrowsAsync<NotFoundException>(() => _service.GetAsync(Guid.NewGuid(), CancellationToken.None));
    }

    [Fact]
    public async Task Delete_UnknownId_ThrowsNotFound()
    {
        await Assert.ThrowsAsync<NotFoundException>(() => _service.DeleteAsync(Guid.NewGuid(), CancellationToken.None));
    }

    [Fact]
    public async Task Delete_ExistingEmployee_RemovesIt()
    {
        var employee = Seed("mine@example.com");

        await _service.DeleteAsync(employee.Id, CancellationToken.None);

        await Assert.ThrowsAsync<NotFoundException>(() => _service.GetAsync(employee.Id, CancellationToken.None));
    }

    [Theory]
    [InlineData(0, 20)]
    [InlineData(1, 0)]
    [InlineData(1, 101)]
    public async Task List_InvalidPaging_ThrowsValidation(int page, int pageSize)
    {
        await Assert.ThrowsAsync<ValidationException>(() =>
            _service.ListAsync(new EmployeeListQuery(page, pageSize), CancellationToken.None));
    }

    [Fact]
    public async Task List_SearchLongerThan100Characters_ThrowsValidation()
    {
        await Assert.ThrowsAsync<ValidationException>(() =>
            _service.ListAsync(new EmployeeListQuery(Search: new string('a', 101)), CancellationToken.None));
    }
}
