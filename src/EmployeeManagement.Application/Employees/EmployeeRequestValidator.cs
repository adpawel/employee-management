using System.Text.RegularExpressions;
using EmployeeManagement.Domain.Employees;
using FluentValidation;

namespace EmployeeManagement.Application.Employees;

// Expects a normalized request. Email uniqueness needs the database, so EmployeeService checks it.
public sealed partial class EmployeeRequestValidator : AbstractValidator<EmployeeRequest>
{
    public static readonly DateOnly MinHireDate = new(1900, 1, 1);

    [GeneratedRegex(@"\A\+[1-9][0-9]{7,14}\z")]
    private static partial Regex PhoneRegex();

    [GeneratedRegex(@"\A[A-Za-z0-9 \-]{3,10}\z")]
    private static partial Regex PincodeRegex();

    public EmployeeRequestValidator(TimeProvider timeProvider)
    {
        RuleFor(x => x.Name).Cascade(CascadeMode.Stop)
            .NotEmpty()
            .MaximumLength(EmployeeConstraints.NameMaxLength)
            .Must(name => !name!.Any(char.IsControl)).WithMessage("'{PropertyName}' must not contain control characters.");

        RuleFor(x => x.Email).Cascade(CascadeMode.Stop)
            .NotEmpty()
            .MaximumLength(EmployeeConstraints.EmailMaxLength)
            .EmailAddress();

        RuleFor(x => x.HireDate).Cascade(CascadeMode.Stop)
            .NotEmpty()
            .Must(value => EmployeeRequest.TryParseHireDate(value, out _))
                .WithMessage($"'{{PropertyName}}' must be a valid date in {EmployeeRequest.HireDateFormat} format.")
            .Must(value => ParseHireDate(value) <= Today(timeProvider))
                .WithMessage("'{PropertyName}' must not be in the future.")
            .Must(value => ParseHireDate(value) >= MinHireDate)
                .WithMessage($"'{{PropertyName}}' must not be earlier than {MinHireDate:yyyy-MM-dd}.");

        RuleFor(x => x.PhoneNo).Cascade(CascadeMode.Stop)
            .NotEmpty()
            .Must(phone => PhoneRegex().IsMatch(phone!))
                .WithMessage("'{PropertyName}' must be in international E.164 format, e.g. +48123456789.");

        RuleFor(x => x.Status).Cascade(CascadeMode.Stop)
            .NotEmpty()
            .Must(status => EmployeeRequest.TryParseStatus(status, out _))
                .WithMessage("'{PropertyName}' must be either 'active' or 'inactive'.");

        RuleFor(x => x.ProfilePicture).Cascade(CascadeMode.Stop)
            .MaximumLength(EmployeeConstraints.ProfilePictureUrlMaxLength)
            .Must(BeHttpUrl).WithMessage("'{PropertyName}' must be an absolute http or https URL.")
            .When(x => x.ProfilePicture is not null);

        RuleFor(x => x.Pincode).Cascade(CascadeMode.Stop)
            .NotEmpty()
            .Must(pincode => PincodeRegex().IsMatch(pincode!))
                .WithMessage("'{PropertyName}' must be 3-10 characters: letters, digits, spaces or hyphens.");

        RuleFor(x => x.Address).NotEmpty().MaximumLength(EmployeeConstraints.AddressMaxLength);
        RuleFor(x => x.State).NotEmpty().MaximumLength(EmployeeConstraints.RegionMaxLength);
        RuleFor(x => x.Country).NotEmpty().MaximumLength(EmployeeConstraints.RegionMaxLength);
        RuleFor(x => x.City).NotEmpty().MaximumLength(EmployeeConstraints.RegionMaxLength);
    }

    private static DateOnly Today(TimeProvider timeProvider) =>
        DateOnly.FromDateTime(timeProvider.GetUtcNow().UtcDateTime);

    private static DateOnly ParseHireDate(string? value)
    {
        EmployeeRequest.TryParseHireDate(value, out var date);
        return date;
    }

    private static bool BeHttpUrl(string? value) =>
        Uri.TryCreate(value, UriKind.Absolute, out var uri)
        && (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps);
}
