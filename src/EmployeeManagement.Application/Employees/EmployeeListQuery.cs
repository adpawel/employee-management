namespace EmployeeManagement.Application.Employees;

public sealed record EmployeeListQuery(int Page = 1, int PageSize = 20, string? Search = null);
