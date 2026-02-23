using FluentValidation;
using FundManager.Contributions.Api.Controllers;

namespace FundManager.Contributions.Api.Validators;

public class GenerateDuesValidator : AbstractValidator<GenerateDuesRequestDto>
{
    public GenerateDuesValidator()
    {
        RuleFor(x => x.MonthYear)
            .GreaterThan(202000).WithMessage("MonthYear must be in YYYYMM format (e.g., 202602).")
            .Must(BeValidMonthYear).WithMessage("MonthYear must have a valid month (01-12).");
    }

    private static bool BeValidMonthYear(int monthYear)
    {
        var month = monthYear % 100;
        return month is >= 1 and <= 12;
    }
}

public class RecordPaymentValidator : AbstractValidator<RecordPaymentRequestDto>
{
    public RecordPaymentValidator()
    {
        RuleFor(x => x.DueId).NotEmpty();
        RuleFor(x => x.Amount).GreaterThan(0).WithMessage("Payment amount must be greater than zero.");
    }
}
