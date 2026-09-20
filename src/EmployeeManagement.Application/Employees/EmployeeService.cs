using System.Text.Json;
using EmployeeManagement.Application.Common.Exceptions;
using EmployeeManagement.Domain.Employees;
using FluentValidation;
using FluentValidation.Results;

namespace EmployeeManagement.Application.Employees;

public sealed class EmployeeService(
    IEmployeeRepository repository,
    IValidator<EmployeeRequest> requestValidator,
    IValidator<EmployeeListQuery> listQueryValidator,
    TimeProvider timeProvider)
{
    public const int MaxImportRows = 1000;

    public async Task<EmployeeResponse> CreateAsync(EmployeeRequest request, CancellationToken ct)
    {
        var normalized = await NormalizeAndValidateAsync(request, ct);
        await EnsureEmailIsUniqueAsync(normalized.Email!, excludeId: null, ct);

        var employee = normalized.ToEmployee(timeProvider.GetUtcNow());

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

        normalized.ApplyTo(employee);

        await repository.SaveChangesAsync(ct);

        return employee.ToResponse();
    }

    public async Task DeleteAsync(Guid id, CancellationToken ct)
    {
        var employee = await FindAsync(id, ct);

        repository.Remove(employee);
        await repository.SaveChangesAsync(ct);
    }

    // Rows are independent, so a bad row is reported and skipped instead of failing the whole file.
    // All valid rows are saved in one SaveChanges (one transaction): a database failure never leaves half an import.
    public async Task<EmployeeImportResult> ImportAsync(IReadOnlyList<EmployeeImportRow> rows, CancellationToken ct)
    {
        if (rows.Count == 0)
        {
            throw EmployeeImportFileError.Create("The file contains no employee rows.");
        }

        if (rows.Count > MaxImportRows)
        {
            throw EmployeeImportFileError.Create($"The file contains more than {MaxImportRows} employee rows.");
        }

        var checkedRows = new List<(int Row, EmployeeRequest Request, Dictionary<string, string[]>? Errors)>(rows.Count);
        // Only valid rows claim an email, so the first valid occurrence wins and later ones point back to it.
        var firstRowByEmail = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

        foreach (var row in rows)
        {
            var normalized = row.Request.Normalize();
            var validation = await requestValidator.ValidateAsync(normalized, ct);

            Dictionary<string, string[]>? errors = null;
            if (!validation.IsValid)
            {
                errors = ToErrors(validation.Errors);
            }
            else if (firstRowByEmail.TryGetValue(normalized.Email!, out var firstRow))
            {
                errors = EmailError($"Duplicate of row {firstRow} in this file.");
            }
            else
            {
                firstRowByEmail.Add(normalized.Email!, row.Row);
            }

            checkedRows.Add((row.Row, normalized, errors));
        }

        // One query for the whole file instead of one per row.
        var existingEmails = firstRowByEmail.Count == 0
            ? new HashSet<string>()
            : new HashSet<string>(
                await repository.GetExistingEmailsAsync(firstRowByEmail.Keys, ct), StringComparer.OrdinalIgnoreCase);

        var results = new List<EmployeeImportRowResult>(rows.Count);
        foreach (var (row, request, rowErrors) in checkedRows)
        {
            var errors = rowErrors;
            if (errors is null && existingEmails.Contains(request.Email!))
            {
                errors = EmailError("An employee with this email already exists.");
            }

            var email = string.IsNullOrEmpty(request.Email) ? null : request.Email;
            if (errors is not null)
            {
                results.Add(new EmployeeImportRowResult(row, email, EmployeeImportRowResult.RejectedStatus, null, errors));
                continue;
            }

            var employee = request.ToEmployee(timeProvider.GetUtcNow());
            repository.Add(employee);
            results.Add(new EmployeeImportRowResult(row, email, EmployeeImportRowResult.ImportedStatus, employee.Id, null));
        }

        var imported = results.Count(r => r.Status == EmployeeImportRowResult.ImportedStatus);
        if (imported > 0)
        {
            // A concurrent POST /employee can still take one of these emails; the unique index then rejects
            // the whole batch as a 409 (see EmployeeRepository), and the client can simply retry the import.
            await repository.SaveChangesAsync(ct);
        }

        return new EmployeeImportResult(rows.Count, imported, rows.Count - imported, results);
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

    // Same shape and camelCase keys as the 400 response produced by ApiExceptionHandler.
    private static Dictionary<string, string[]> ToErrors(IEnumerable<ValidationFailure> failures) =>
        failures
            .GroupBy(f => JsonNamingPolicy.CamelCase.ConvertName(f.PropertyName))
            .ToDictionary(g => g.Key, g => g.Select(f => f.ErrorMessage).ToArray());

    private static Dictionary<string, string[]> EmailError(string message) =>
        new() { [JsonNamingPolicy.CamelCase.ConvertName(nameof(EmployeeRequest.Email))] = [message] };
}
