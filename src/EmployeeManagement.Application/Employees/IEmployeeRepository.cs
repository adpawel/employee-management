using EmployeeManagement.Domain.Employees;

namespace EmployeeManagement.Application.Employees;

public interface IEmployeeRepository
{
    Task<Employee?> GetByIdAsync(Guid id, CancellationToken ct);

    Task<(IReadOnlyList<Employee> Items, int TotalCount)> ListAsync(EmployeeListQuery query, CancellationToken ct);

    Task<bool> EmailExistsAsync(string email, Guid? excludeId, CancellationToken ct);

    Task<IReadOnlyList<string>> GetExistingEmailsAsync(IReadOnlyCollection<string> emails, CancellationToken ct);

    void Add(Employee employee);

    void Remove(Employee employee);

    Task SaveChangesAsync(CancellationToken ct);
}
