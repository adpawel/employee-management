using EmployeeManagement.Domain.Employees;

namespace EmployeeManagement.Application.Employees;

public interface IEmployeeRepository
{
    Task<Employee?> GetByIdAsync(Guid id, CancellationToken ct);

    Task<IReadOnlyList<Employee>> ListAsync(CancellationToken ct);

    Task<bool> EmailExistsAsync(string email, Guid? excludeId, CancellationToken ct);

    void Add(Employee employee);

    void Remove(Employee employee);

    Task SaveChangesAsync(CancellationToken ct);
}
