namespace EmployeeManagement.Application.Employees;

public interface IEmployeeCsvReader
{
    Task<IReadOnlyList<EmployeeImportRow>> ReadAsync(Stream csv, CancellationToken ct);
}
