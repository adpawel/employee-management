namespace EmployeeManagement.Application.Employees;

// Row is the line number in the file (header = 1), so it matches what the user sees in a spreadsheet.
public sealed record EmployeeImportRow(int Row, EmployeeRequest Request);
