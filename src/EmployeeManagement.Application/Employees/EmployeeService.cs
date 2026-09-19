using EmployeeManagement.Application.Common.Exceptions;
using EmployeeManagement.Domain.Employees;
using FluentValidation;

namespace EmployeeManagement.Application.Employees;

public sealed class EmployeeService(
    IEmployeeRepository repository,
    IValidator<EmployeeRequest> requestValidator,
    IValidator<EmployeeListQuery> listQueryValidator,
    TimeProvider timeProvider)
{
    public async Task<EmployeeResponse> CreateAsync(EmployeeRequest request, CancellationToken ct)
    {
        var normalized = await NormalizeAndValidateAsync(request, ct);
        await EnsureEmailIsUniqueAsync(normalized.Email!, excludeId: null, ct);

        var employee = Employee.Create(
            normalized.Name!,
            ParseHireDate(normalized.HireDate),
            normalized.Email!,
            normalized.PhoneNo!,
            normalized.ProfilePicture,
            ParseStatus(normalized.Status),
            normalized.Address!,
            normalized.State!,
            normalized.Country!,
            normalized.City!,
            normalized.Pincode!,
            timeProvider.GetUtcNow());

        repository.Add(employee);
        await repository.SaveChangesAsync(ct);

        return employee.ToResponse();
    }

    public async Task<EmployeeResponse> GetAsync(Guid id, CancellationToken ct) =>
        (await FindAsync(id, ct)).ToResponse();

    public async Task<PagedResult<EmployeeResponse>> ListAsync(EmployeeListQuery query, CancellationToken ct)
    {
        query = query with { Search = query.Search?.Trim() };
        await listQueryValidator.ValidateAndThrowAsync(query, ct);

        var (items, totalCount) = await repository.ListAsync(query, ct);

        return new PagedResult<EmployeeResponse>(
            items.Select(e => e.ToResponse()).ToList(), query.Page, query.PageSize, totalCount);
    }

    public async Task<EmployeeResponse> UpdateAsync(Guid id, EmployeeRequest request, CancellationToken ct)
    {
        var employee = await FindAsync(id, ct);

        var normalized = await NormalizeAndValidateAsync(request, ct);
        await EnsureEmailIsUniqueAsync(normalized.Email!, excludeId: id, ct);

        employee.Update(
            normalized.Name!,
            ParseHireDate(normalized.HireDate),
            normalized.Email!,
            normalized.PhoneNo!,
            normalized.ProfilePicture,
            ParseStatus(normalized.Status),
            normalized.Address!,
            normalized.State!,
            normalized.Country!,
            normalized.City!,
            normalized.Pincode!);

        await repository.SaveChangesAsync(ct);

        return employee.ToResponse();
    }

    public async Task DeleteAsync(Guid id, CancellationToken ct)
    {
        var employee = await FindAsync(id, ct);

        repository.Remove(employee);
        await repository.SaveChangesAsync(ct);
    }

    private async Task<Employee> FindAsync(Guid id, CancellationToken ct) =>
        await repository.GetByIdAsync(id, ct)
        ?? throw new NotFoundException($"Employee '{id}' was not found.");

    private async Task<EmployeeRequest> NormalizeAndValidateAsync(EmployeeRequest request, CancellationToken ct)
    {
        var normalized = request.Normalize();
        await requestValidator.ValidateAndThrowAsync(normalized, ct);
        return normalized;
    }

    // Concurrent requests can both pass this check; the unique index then stops the second one (see EmployeeRepository).
    private async Task EnsureEmailIsUniqueAsync(string email, Guid? excludeId, CancellationToken ct)
    {
        if (await repository.EmailExistsAsync(email, excludeId, ct))
        {
            throw new ConflictException("An employee with this email already exists.");
        }
    }

    private static DateOnly ParseHireDate(string? hireDate)
    {
        EmployeeRequest.TryParseHireDate(hireDate, out var date);
        return date;
    }

    private static EmployeeStatus ParseStatus(string? status)
    {
        EmployeeRequest.TryParseStatus(status, out var parsed);
        return parsed;
    }
}
