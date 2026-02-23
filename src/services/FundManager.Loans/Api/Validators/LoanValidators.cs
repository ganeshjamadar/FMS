using FluentValidation;
using FundManager.Loans.Api.Controllers;

namespace FundManager.Loans.Api.Validators;

public class LoanRequestValidator : AbstractValidator<LoanRequestDto>
{
    public LoanRequestValidator()
    {
        RuleFor(x => x.PrincipalAmount)
            .GreaterThan(0).WithMessage("Principal amount must be positive.");

        RuleFor(x => x.RequestedStartMonth)
            .Must(BeValidMonthYear).WithMessage("RequestedStartMonth must be in YYYYMM format (e.g. 202601).");
    }

    private static bool BeValidMonthYear(int monthYear)
    {
        var year = monthYear / 100;
        var month = monthYear % 100;
        return year >= 2020 && year <= 2100 && month >= 1 && month <= 12;
    }
}

public class ApproveLoanValidator : AbstractValidator<ApproveLoanRequestDto>
{
    public ApproveLoanValidator()
    {
        RuleFor(x => x.ScheduledInstallment)
            .GreaterThanOrEqualTo(0).WithMessage("Scheduled installment must be non-negative.");
    }
}

public class RejectLoanValidator : AbstractValidator<RejectLoanRequestDto>
{
    public RejectLoanValidator()
    {
        RuleFor(x => x.Reason)
            .NotEmpty().WithMessage("Rejection reason is required.");
    }
}
