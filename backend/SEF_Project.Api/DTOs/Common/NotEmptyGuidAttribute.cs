using System.ComponentModel.DataAnnotations;

namespace SEF_Project.Api.DTOs.Common;

public class NotEmptyGuidAttribute : ValidationAttribute
{
    protected override ValidationResult? IsValid(
        object? value,
        ValidationContext validationContext)
    {
        if (value is Guid guid && guid != Guid.Empty)
        {
            return ValidationResult.Success;
        }

        return new ValidationResult(
            ErrorMessage ?? "A valid identifier is required.",
            new[] { validationContext.MemberName! });
    }
}
