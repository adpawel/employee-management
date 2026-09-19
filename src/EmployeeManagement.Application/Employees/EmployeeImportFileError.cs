using FluentValidation;
using FluentValidation.Results;

namespace EmployeeManagement.Application.Employees;

// A problem with the file as a whole rejects the entire import with 400, unlike a problem with a single row.
public static class EmployeeImportFileError
{
    public const string Key = "file";

    public static ValidationException Create(string message) => new([new ValidationFailure(Key, message)]);
}
