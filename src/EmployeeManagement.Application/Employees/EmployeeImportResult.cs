namespace EmployeeManagement.Application.Employees;

public sealed record EmployeeImportResult(
    int Total,
    int Imported,
    int Rejected,
    IReadOnlyList<EmployeeImportRowResult> Results);

public sealed record EmployeeImportRowResult(
    int Row,
    string? Email,
    string Status,
    Guid? Id,
    IDictionary<string, string[]>? Errors)
{
    public const string ImportedStatus = "imported";
    public const string RejectedStatus = "rejected";
}
