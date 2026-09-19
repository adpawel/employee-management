using EmployeeManagement.Application.Employees;
using EmployeeManagement.Domain.Employees;
using Microsoft.EntityFrameworkCore;

namespace EmployeeManagement.Infrastructure.Persistence;

// Queries go exclusively through LINQ, which EF Core translates into parameterized SQL,
// so user input never gets concatenated into a query string.
internal class EmployeeRepository(AppDbContext db) : IEmployeeRepository
{
    public Task<Employee?> GetByIdAsync(Guid id, CancellationToken ct) =>
        db.Employees.FirstOrDefaultAsync(e => e.Id == id, ct);

    public async Task<IReadOnlyList<Employee>> ListAsync(CancellationToken ct) =>
        await db.Employees.AsNoTracking().OrderBy(e => e.Name).ToListAsync(ct);

    public Task<bool> EmailExistsAsync(string email, Guid? excludeId, CancellationToken ct) =>
        db.Employees.AnyAsync(e => e.Email == email && e.Id != excludeId, ct);

    public void Add(Employee employee) => db.Employees.Add(employee);

    public void Remove(Employee employee) => db.Employees.Remove(employee);

    public Task SaveChangesAsync(CancellationToken ct) => db.SaveChangesAsync(ct);
}
