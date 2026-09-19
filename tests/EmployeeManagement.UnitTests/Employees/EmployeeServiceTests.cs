using EmployeeManagement.Application.Common.Exceptions;
using EmployeeManagement.Application.Employees;
using EmployeeManagement.Domain.Employees;
using FluentValidation;
using Microsoft.Extensions.Time.Testing;
using NSubstitute;

namespace EmployeeManagement.UnitTests.Employees;

public class EmployeeServiceTests
{
    private static readonly DateTimeOffset CreatedAt = new(2025, 1, 10, 9, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset Now = new(2025, 6, 15, 12, 0, 0, TimeSpan.Zero);

    private readonly IEmployeeRepository _repository = Substitute.For<IEmployeeRepository>();
    private readonly List<Employee> _added = [];
    private readonly EmployeeService _service;

    public EmployeeServiceTests()
    {
        _repository.When(r => r.Add(Arg.Any<Employee>())).Do(call => _added.Add(call.Arg<Employee>()));
        GivenExistingEmails();

        var timeProvider = new FakeTimeProvider(Now);
        _service = new EmployeeService(
            _repository,
            new EmployeeRequestValidator(timeProvider),
            new EmployeeListQueryValidator(),
            timeProvider);
    }

    private static EmployeeRequest Request(string email = "jane.doe@example.com") =>
        EmployeeRequestValidatorTests.ValidRequest() with { Email = email };

    private Employee GivenEmployee(string email)
    {
        var employee = Employee.Create(
            "Seeded Person", new DateOnly(2020, 1, 1), email, "+15550100000", null, EmployeeStatus.Active,
            "Somewhere 1", "State", "Country", "City", "12345", CreatedAt);
        _repository.GetByIdAsync(employee.Id, Arg.Any<CancellationToken>()).Returns(employee);
        return employee;
    }

    private void GivenEmailTaken(string email) =>
        _repository.EmailExistsAsync(email, Arg.Any<Guid?>(), Arg.Any<CancellationToken>()).Returns(true);

    private void GivenExistingEmails(params string[] emails) =>
        _repository.GetExistingEmailsAsync(default!, default)
            .ReturnsForAnyArgs(Task.FromResult<IReadOnlyList<string>>(emails));

    [Fact]
    public async Task Create_SetsIdAndCreatedAtItself_AndSaves()
    {
        var response = await _service.CreateAsync(Request(), CancellationToken.None);

        Assert.NotEqual(Guid.Empty, response.Id);
        Assert.Equal(Now, response.CreatedAt);
        Assert.Equal(response.Id, Assert.Single(_added).Id);
        await _repository.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Create_DuplicateEmail_ThrowsConflict_AndDoesNotSave()
    {
        GivenEmailTaken("jane.doe@example.com");

        await Assert.ThrowsAsync<ConflictException>(() => _service.CreateAsync(Request(), CancellationToken.None));

        Assert.Empty(_added);
        await _repository.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Update_UnknownId_IsReportedBeforeInvalidBody()
    {
        var invalid = Request() with { Email = "not-an-email" };

        await Assert.ThrowsAsync<NotFoundException>(() =>
            _service.UpdateAsync(Guid.NewGuid(), invalid, CancellationToken.None));
    }

    [Fact]
    public async Task Update_EmailOfAnotherEmployee_ThrowsConflict_AndChangesNothing()
    {
        var employee = GivenEmployee("mine@example.com");
        GivenEmailTaken("taken@example.com");

        await Assert.ThrowsAsync<ConflictException>(() =>
            _service.UpdateAsync(employee.Id, Request("taken@example.com"), CancellationToken.None));

        Assert.Equal("mine@example.com", employee.Email);
        await _repository.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Update_ChecksEmailUniqueness_ExcludingTheEmployeeItself()
    {
        var employee = GivenEmployee("mine@example.com");

        await _service.UpdateAsync(employee.Id, Request("mine@example.com"), CancellationToken.None);

        await _repository.Received(1).EmailExistsAsync("mine@example.com", employee.Id, Arg.Any<CancellationToken>());
        await _repository.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Update_ReplacesEditableFields_WithNormalizedValues_ButKeepsIdAndCreatedAt()
    {
        var employee = GivenEmployee("mine@example.com");
        var id = employee.Id;
        var request = Request("  Jane.Doe@Example.com ") with
        {
            Name = "  Jane Doe-Smith ",
            PhoneNo = "+1 (555) 010-1234",
            ProfilePicture = null,
            Status = "Inactive"
        };

        var response = await _service.UpdateAsync(employee.Id, request, CancellationToken.None);

        Assert.Equal("Jane Doe-Smith", response.Name);
        Assert.Equal("jane.doe@example.com", response.Email);
        Assert.Equal("+15550101234", response.PhoneNo);
        Assert.Null(response.ProfilePicture);
        Assert.Equal("inactive", response.Status);
        Assert.Equal(id, response.Id);
        Assert.Equal(CreatedAt, response.CreatedAt);
    }

    [Fact]
    public async Task Update_InvalidRequest_ThrowsValidation_AndChangesNothing()
    {
        var employee = GivenEmployee("mine@example.com");

        var ex = await Assert.ThrowsAsync<ValidationException>(() =>
            _service.UpdateAsync(employee.Id, Request("mine@example.com") with { Status = "unknown" }, CancellationToken.None));

        Assert.Contains(ex.Errors, e => e.PropertyName == nameof(EmployeeRequest.Status));
        Assert.Equal("Seeded Person", employee.Name);
        await _repository.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Delete_UnknownId_ThrowsNotFound()
    {
        await Assert.ThrowsAsync<NotFoundException>(() => _service.DeleteAsync(Guid.NewGuid(), CancellationToken.None));

        _repository.DidNotReceiveWithAnyArgs().Remove(default!);
    }

    [Theory]
    [InlineData(0, 20)]
    [InlineData(1, 101)]
    public async Task List_InvalidPaging_ThrowsValidation(int page, int pageSize)
    {
        await Assert.ThrowsAsync<ValidationException>(() =>
            _service.ListAsync(new EmployeeListQuery(page, pageSize), CancellationToken.None));
    }

    // Bulk import: data rows start at line 2, right after the header.
    private static EmployeeImportRow Row(int row, string email, Func<EmployeeRequest, EmployeeRequest>? change = null) =>
        new(row, change is null ? Request(email) : change(Request(email)));

    private Task<EmployeeImportResult> ImportAsync(params EmployeeImportRow[] rows) =>
        _service.ImportAsync(rows, CancellationToken.None);

    [Fact]
    public async Task Import_MixedRows_SavesValidOnesInOneSave_AndReportsInvalidOnesPerField()
    {
        var result = await ImportAsync(
            Row(2, "a@example.com"),
            Row(3, "b@example.com", r => r with { HireDate = "2079-07-01" }),
            Row(4, "c@example.com", r => r with { PhoneNo = "12345", Status = "unknown" }),
            Row(5, "d@example.com"));

        Assert.Equal((4, 2, 2), (result.Total, result.Imported, result.Rejected));
        Assert.Equal([2, 3, 4, 5], result.Results.Select(r => r.Row));

        var imported = result.Results.Where(r => r.Status == EmployeeImportRowResult.ImportedStatus).ToList();
        Assert.Equal(["a@example.com", "d@example.com"], imported.Select(r => r.Email));
        Assert.Equal(imported.Select(r => r.Id!.Value), _added.Select(e => e.Id));
        Assert.All(_added, e => Assert.Equal(Now, e.CreatedAt));

        var futureDate = result.Results.Single(r => r.Row == 3);
        Assert.Equal(EmployeeImportRowResult.RejectedStatus, futureDate.Status);
        Assert.Null(futureDate.Id);
        Assert.Equal(["hireDate"], futureDate.Errors!.Keys);
        Assert.Equal(["phoneNo", "status"], result.Results.Single(r => r.Row == 4).Errors!.Keys.Order());

        // All valid rows go in one SaveChanges, i.e. one transaction.
        await _repository.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Import_DuplicateEmailInFile_FirstWins_AndLaterRowPointsToIt_IgnoringCase()
    {
        var result = await ImportAsync(
            Row(2, "dup@example.com"),
            Row(3, "other@example.com"),
            Row(4, "DUP@Example.com"));

        Assert.Equal(2, result.Imported);
        var duplicate = result.Results.Single(r => r.Row == 4);
        Assert.Equal(EmployeeImportRowResult.RejectedStatus, duplicate.Status);
        Assert.Contains("row 2", Assert.Single(duplicate.Errors!["email"]));
    }

    [Fact]
    public async Task Import_InvalidFirstOccurrence_DoesNotBlockLaterValidRowWithSameEmail()
    {
        var result = await ImportAsync(
            Row(2, "same@example.com", r => r with { HireDate = "2019-13-12" }),
            Row(3, "same@example.com"));

        Assert.Equal(EmployeeImportRowResult.RejectedStatus, result.Results[0].Status);
        Assert.Equal(EmployeeImportRowResult.ImportedStatus, result.Results[1].Status);
    }

    [Fact]
    public async Task Import_EmailAlreadyInDatabase_RejectsRow_UsingOneQueryForTheWholeFile()
    {
        GivenExistingEmails("taken@example.com");

        var result = await ImportAsync(
            Row(2, "new@example.com"),
            Row(3, "Taken@Example.com"),
            Row(4, "another@example.com"));

        Assert.Equal((2, 1), (result.Imported, result.Rejected));
        Assert.Contains("already exists", Assert.Single(result.Results.Single(r => r.Row == 3).Errors!["email"]));
        await _repository.ReceivedWithAnyArgs(1).GetExistingEmailsAsync(default!, default);
    }

    [Fact]
    public async Task Import_AllRowsInvalid_ReturnsReport_AndDoesNotSave()
    {
        var result = await ImportAsync(
            Row(2, "a@example.com", r => r with { HireDate = "1899-02-28" }),
            Row(3, "not-an-email"));

        Assert.Equal((2, 0, 2), (result.Total, result.Imported, result.Rejected));
        Assert.Empty(_added);
        await _repository.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Theory]
    [InlineData(0)]
    [InlineData(EmployeeService.MaxImportRows + 1)]
    public async Task Import_EmptyOrTooLargeFile_ThrowsFileError_AndSavesNothing(int rowCount)
    {
        var rows = Enumerable.Range(2, rowCount).Select(i => Row(i, $"user{i}@example.com")).ToArray();

        var ex = await Assert.ThrowsAsync<ValidationException>(() => ImportAsync(rows));

        Assert.Equal(EmployeeImportFileError.Key, Assert.Single(ex.Errors).PropertyName);
        Assert.Empty(_added);
    }
}
