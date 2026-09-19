using EmployeeManagement.Application.Employees;
using EmployeeManagement.Domain.Employees;

namespace EmployeeManagement.UnitTests.Employees;

internal sealed class FakeEmployeeRepository : IEmployeeRepository
{
    private readonly List<Employee> _employees = [];

    public int SaveCount { get; private set; }

    public Task<Employee?> GetByIdAsync(Guid id, CancellationToken ct) =>
        Task.FromResult(_employees.FirstOrDefault(e => e.Id == id));

    // Search and paging run in SQL, so they are covered by the integration tests instead.
    public Task<(IReadOnlyList<Employee> Items, int TotalCount)> ListAsync(EmployeeListQuery query, CancellationToken ct) =>
        throw new NotSupportedException();

    public Task<bool> EmailExistsAsync(string email, Guid? excludeId, CancellationToken ct) =>
        Task.FromResult(_employees.Any(e =>
            string.Equals(e.Email, email, StringComparison.OrdinalIgnoreCase) && e.Id != excludeId));

    public void Add(Employee employee) => _employees.Add(employee);

    public void Remove(Employee employee) => _employees.Remove(employee);

    public Task SaveChangesAsync(CancellationToken ct)
    {
        SaveCount++;
        return Task.CompletedTask;
    }
}
