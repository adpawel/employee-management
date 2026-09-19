using System.Globalization;
using EmployeeManagement.Domain.Employees;

namespace EmployeeManagement.Application.Employees;

// Strings and nullable on purpose: the same validator serves JSON and CSV rows, and a bad or missing value
// becomes a per-field validation error instead of a generic deserialization failure.
public sealed record EmployeeRequest(
    string? Name,
    string? HireDate,
    string? Email,
    string? PhoneNo,
    string? ProfilePicture,
    string? Status,
    string? Address,
    string? State,
    string? Country,
    string? City,
    string? Pincode)
{
    public const string HireDateFormat = "yyyy-MM-dd";

    public EmployeeRequest Normalize() => this with
    {
        Name = Trim(Name),
        HireDate = Trim(HireDate),
        Email = Trim(Email)?.ToLowerInvariant(),
        PhoneNo = StripPhoneSeparators(Trim(PhoneNo)),
        ProfilePicture = Trim(ProfilePicture) is { Length: > 0 } url ? url : null,
        Status = Trim(Status),
        Address = Trim(Address),
        State = Trim(State),
        Country = Trim(Country),
        City = Trim(City),
        Pincode = Trim(Pincode)
    };

    public static bool TryParseHireDate(string? value, out DateOnly date) =>
        DateOnly.TryParseExact(value, HireDateFormat, CultureInfo.InvariantCulture, DateTimeStyles.None, out date);

    // Not Enum.TryParse: it would also accept numbers such as "1".
    public static bool TryParseStatus(string? value, out EmployeeStatus status)
    {
        if (string.Equals(value, "active", StringComparison.OrdinalIgnoreCase))
        {
            status = EmployeeStatus.Active;
            return true;
        }

        if (string.Equals(value, "inactive", StringComparison.OrdinalIgnoreCase))
        {
            status = EmployeeStatus.Inactive;
            return true;
        }

        status = default;
        return false;
    }

    private static string? Trim(string? value) => value?.Trim();

    private static string? StripPhoneSeparators(string? value) =>
        value is null ? null : string.Concat(value.Where(c => c is not (' ' or '-' or '(' or ')')));
}
