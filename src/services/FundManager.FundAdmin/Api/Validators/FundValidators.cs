using FluentValidation;
using FundManager.FundAdmin.Api.Controllers;

namespace FundManager.FundAdmin.Api.Validators;

public class CreateFundValidator : AbstractValidator<CreateFundRequestDto>
{
    public CreateFundValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("Fund name is required.")
            .MaximumLength(255).WithMessage("Fund name must not exceed 255 characters.");

        RuleFor(x => x.MonthlyInterestRate)
            .GreaterThan(0).WithMessage("Monthly interest rate must be > 0.")
            .LessThanOrEqualTo(1.0m).WithMessage("Monthly interest rate must be ≤ 1.0 (100%).");

        RuleFor(x => x.MinimumMonthlyContribution)
            .GreaterThan(0).WithMessage("Minimum monthly contribution must be > 0.");

        RuleFor(x => x.MinimumPrincipalPerRepayment)
            .GreaterThan(0).WithMessage("Minimum principal per repayment must be > 0.");

        RuleFor(x => x.LoanApprovalPolicy)
            .Must(p => p is null or "AdminOnly" or "AdminWithVoting")
            .WithMessage("Loan approval policy must be 'AdminOnly' or 'AdminWithVoting'.");

        RuleFor(x => x.MaxLoanPerMember)
            .GreaterThan(0).When(x => x.MaxLoanPerMember.HasValue)
            .WithMessage("Max loan per member must be > 0 if specified.");

        RuleFor(x => x.MaxConcurrentLoans)
            .GreaterThan(0).When(x => x.MaxConcurrentLoans.HasValue)
            .WithMessage("Max concurrent loans must be > 0 if specified.");

        RuleFor(x => x.OverduePenaltyType)
            .Must(p => p is null or "None" or "Flat" or "Percentage")
            .WithMessage("Overdue penalty type must be 'None', 'Flat', or 'Percentage'.");

        RuleFor(x => x.OverduePenaltyValue)
            .GreaterThanOrEqualTo(0).WithMessage("Overdue penalty value must be ≥ 0.");

        RuleFor(x => x.ContributionDayOfMonth)
            .InclusiveBetween(1, 28).WithMessage("Contribution day must be between 1 and 28.");

        RuleFor(x => x.GracePeriodDays)
            .GreaterThanOrEqualTo(0).WithMessage("Grace period days must be ≥ 0.");
    }
}

public class UpdateFundValidator : AbstractValidator<UpdateFundRequestDto>
{
    public UpdateFundValidator()
    {
        // Description is always updatable — no constraints beyond optionality
        // Config fields validated here for format; Draft-state gate is enforced in the domain entity

        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("Fund name cannot be empty.")
            .MaximumLength(255).WithMessage("Fund name must not exceed 255 characters.")
            .When(x => x.Name is not null);

        RuleFor(x => x.MonthlyInterestRate)
            .GreaterThan(0).WithMessage("Monthly interest rate must be > 0.")
            .LessThanOrEqualTo(1.0m).WithMessage("Monthly interest rate must be ≤ 1.0 (100%).")
            .When(x => x.MonthlyInterestRate.HasValue);

        RuleFor(x => x.MinimumMonthlyContribution)
            .GreaterThan(0).WithMessage("Minimum monthly contribution must be > 0.")
            .When(x => x.MinimumMonthlyContribution.HasValue);

        RuleFor(x => x.MinimumPrincipalPerRepayment)
            .GreaterThan(0).WithMessage("Minimum principal per repayment must be > 0.")
            .When(x => x.MinimumPrincipalPerRepayment.HasValue);

        RuleFor(x => x.LoanApprovalPolicy)
            .Must(p => p is "AdminOnly" or "AdminWithVoting")
            .WithMessage("Loan approval policy must be 'AdminOnly' or 'AdminWithVoting'.")
            .When(x => x.LoanApprovalPolicy is not null);

        RuleFor(x => x.MaxLoanPerMember)
            .GreaterThan(0).WithMessage("Max loan per member must be > 0 if specified.")
            .When(x => x.MaxLoanPerMember.HasValue);

        RuleFor(x => x.MaxConcurrentLoans)
            .GreaterThan(0).WithMessage("Max concurrent loans must be > 0 if specified.")
            .When(x => x.MaxConcurrentLoans.HasValue);

        RuleFor(x => x.OverduePenaltyType)
            .Must(p => p is "None" or "Flat" or "Percentage")
            .WithMessage("Overdue penalty type must be 'None', 'Flat', or 'Percentage'.")
            .When(x => x.OverduePenaltyType is not null);

        RuleFor(x => x.OverduePenaltyValue)
            .GreaterThanOrEqualTo(0).WithMessage("Overdue penalty value must be ≥ 0.")
            .When(x => x.OverduePenaltyValue.HasValue);

        RuleFor(x => x.ContributionDayOfMonth)
            .InclusiveBetween(1, 28).WithMessage("Contribution day must be between 1 and 28.")
            .When(x => x.ContributionDayOfMonth.HasValue);

        RuleFor(x => x.GracePeriodDays)
            .GreaterThanOrEqualTo(0).WithMessage("Grace period days must be ≥ 0.")
            .When(x => x.GracePeriodDays.HasValue);
    }
}
