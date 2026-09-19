using EmployeeManagement.Application.Employees;
using EmployeeManagement.Domain.Employees;

namespace EmployeeManagement.UnitTests.Employees;

internal sealed class FakeEmployeeRepository : IEmployeeRepository
{
    private readonly List<Employee> _employees = [];

    public int SaveCount { get; private set; }

    public Task<Employee?> GetByIdAsync(Guid id, CancellationToken ct) =>
        Task.FromResult(_employees.FirstOrDefault(e => e.Id == id));

    public Task<(IReadOnlyList<Employee> Items, int TotalCount)> ListAsync(EmployeeListQuery query, CancellationToken ct)
    {
        var matching = _employees
            .Where(e => string.IsNullOrEmpty(query.Search)
                || e.Name.Contains(query.Search, StringComparison.OrdinalIgnoreCase)
                || e.Email.Contains(query.Search, StringComparison.OrdinalIgnoreCase)
                || e.City.Contains(query.Search, StringComparison.OrdinalIgnoreCase))
            .OrderBy(e => e.Name)
            .ThenBy(e => e.Id)
            .ToList();

        IReadOnlyList<Employee> page = matching.Skip((query.Page - 1) * query.PageSize).Take(query.PageSize).ToList();
        return Task.FromResult((page, matching.Count));
    }

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
