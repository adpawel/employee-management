namespace EmployeeManagement.Application.Employees;

public sealed record EmployeeResponse(
    Guid Id,
    string Name,
    DateOnly HireDate,
    string Email,
    string PhoneNo,
    string? ProfilePicture,
    string Status,
    string Address,
    string State,
    string Country,
    string City,
    string Pincode,
    DateTimeOffset CreatedAt);
