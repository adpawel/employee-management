using FluentValidation;

namespace EmployeeManagement.Application.Employees;

public sealed class EmployeeListQueryValidator : AbstractValidator<EmployeeListQuery>
{
    public const int MaxPageSize = 100;
    public const int MaxSearchLength = 100;

    // Keeps (page - 1) * pageSize away from int overflow.
    private const int MaxPage = 1_000_000;

    public EmployeeListQueryValidator()
    {
        RuleFor(x => x.Page).InclusiveBetween(1, MaxPage);
        RuleFor(x => x.PageSize).InclusiveBetween(1, MaxPageSize);
        RuleFor(x => x.Search).MaximumLength(MaxSearchLength);
    }
}
