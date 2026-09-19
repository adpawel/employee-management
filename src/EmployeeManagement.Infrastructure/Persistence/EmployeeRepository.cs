using EmployeeManagement.Application.Common.Exceptions;
using EmployeeManagement.Application.Employees;
using EmployeeManagement.Domain.Employees;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace EmployeeManagement.Infrastructure.Persistence;

// LINQ only: EF Core generates parameterized SQL, so user input is never concatenated into a query.
internal class EmployeeRepository(AppDbContext db) : IEmployeeRepository
{
    private const int UniqueIndexViolation = 2601;
    private const int UniqueConstraintViolation = 2627;

    public Task<Employee?> GetByIdAsync(Guid id, CancellationToken ct) =>
        db.Employees.FirstOrDefaultAsync(e => e.Id == id, ct);

    public async Task<(IReadOnlyList<Employee> Items, int TotalCount)> ListAsync(
        EmployeeListQuery query, CancellationToken ct)
    {
        var employees = db.Employees.AsNoTracking();

        if (!string.IsNullOrEmpty(query.Search))
        {
            // Wildcards in the input are escaped; case-insensitivity comes from the DB collation.
            var search = query.Search;
            employees = employees.Where(e =>
                e.Name.Contains(search) || e.Email.Contains(search) || e.City.Contains(search));
        }

        var totalCount = await employees.CountAsync(ct);

        // Id as tie-breaker keeps paging stable when names repeat.
        var items = await employees
            .OrderBy(e => e.Name)
            .ThenBy(e => e.Id)
            .Skip((query.Page - 1) * query.PageSize)
            .Take(query.PageSize)
            .ToListAsync(ct);

        return (items, totalCount);
    }

    public Task<bool> EmailExistsAsync(string email, Guid? excludeId, CancellationToken ct) =>
        db.Employees.AnyAsync(e => e.Email == email && e.Id != excludeId, ct);

    public void Add(Employee employee) => db.Employees.Add(employee);

    public void Remove(Employee employee) => db.Employees.Remove(employee);

    public async Task SaveChangesAsync(CancellationToken ct)
    {
        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException ex) when (ex.InnerException is SqlException
        {
            Number: UniqueIndexViolation or UniqueConstraintViolation
        })
        {
            // Email is the only unique index, so this is a concurrent duplicate email.
            throw new ConflictException("An employee with this email already exists.", ex);
        }
    }
}
