namespace EmployeeManagement.Domain.Employees;

public sealed class Employee
{
    private Employee()
    {
    }

    public Guid Id { get; private set; }
    public string Name { get; private set; } = null!;
    public DateOnly HireDate { get; private set; }
    public string Email { get; private set; } = null!;
    public string PhoneNo { get; private set; } = null!;
    public string? ProfilePictureUrl { get; private set; }
    public EmployeeStatus Status { get; private set; }
    public string Address { get; private set; } = null!;
    public string State { get; private set; } = null!;
    public string Country { get; private set; } = null!;
    public string City { get; private set; } = null!;
    public string Pincode { get; private set; } = null!;
    public DateTimeOffset CreatedAt { get; private set; }

    // Values are expected to be validated in the Application layer before reaching the entity.
    // Id and CreatedAt are always system-generated, never taken from client input.
    public static Employee Create(
        string name,
        DateOnly hireDate,
        string email,
        string phoneNo,
        string? profilePictureUrl,
        EmployeeStatus status,
        string address,
        string state,
        string country,
        string city,
        string pincode,
        DateTimeOffset createdAt)
    {
        return new Employee
        {
            // Version 7 GUIDs are unguessable like v4 but time-ordered, which keeps the clustered index from fragmenting.
            Id = Guid.CreateVersion7(createdAt),
            Name = name,
            HireDate = hireDate,
            Email = email,
            PhoneNo = phoneNo,
            ProfilePictureUrl = profilePictureUrl,
            Status = status,
            Address = address,
            State = state,
            Country = country,
            City = city,
            Pincode = pincode,
            CreatedAt = createdAt
        };
    }
}
