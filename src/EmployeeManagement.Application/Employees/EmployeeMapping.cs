using EmployeeManagement.Domain.Employees;

namespace EmployeeManagement.Application.Employees;

internal static class EmployeeMapping
{
    public static EmployeeResponse ToResponse(this Employee employee) => new(
        employee.Id,
        employee.Name,
        employee.HireDate,
        employee.Email,
        employee.PhoneNo,
        employee.ProfilePictureUrl,
        employee.Status.ToString().ToLowerInvariant(),
        employee.Address,
        employee.State,
        employee.Country,
        employee.City,
        employee.Pincode,
        employee.CreatedAt);
}
