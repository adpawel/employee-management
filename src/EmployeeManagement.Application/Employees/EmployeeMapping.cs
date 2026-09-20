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

    public static Employee ToEmployee(this EmployeeRequest request, DateTimeOffset createdAt) => Employee.Create(
        request.Name!,
        ParseHireDate(request.HireDate),
        request.Email!,
        request.PhoneNo!,
        request.ProfilePicture,
        ParseStatus(request.Status),
        request.Address!,
        request.State!,
        request.Country!,
        request.City!,
        request.Pincode!,
        createdAt);

    // PUT is a full replacement, so every editable field is overwritten; Id and CreatedAt stay untouched.
    public static void ApplyTo(this EmployeeRequest request, Employee employee) => employee.Update(
        request.Name!,
        ParseHireDate(request.HireDate),
        request.Email!,
        request.PhoneNo!,
        request.ProfilePicture,
        ParseStatus(request.Status),
        request.Address!,
        request.State!,
        request.Country!,
        request.City!,
        request.Pincode!);

    private static DateOnly ParseHireDate(string? hireDate)
    {
        EmployeeRequest.TryParseHireDate(hireDate, out var date);
        return date;
    }

    private static EmployeeStatus ParseStatus(string? status)
    {
        EmployeeRequest.TryParseStatus(status, out var parsed);
        return parsed;
    }
}
