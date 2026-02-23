using FluentValidation;
using FundManager.FundAdmin.Api.Controllers;

namespace FundManager.FundAdmin.Api.Validators;

public class InviteMemberValidator : AbstractValidator<InviteMemberRequestDto>
{
    public InviteMemberValidator()
    {
        RuleFor(x => x.TargetContact)
            .NotEmpty().WithMessage("Target contact (phone or email) is required.")
            .MaximumLength(255).WithMessage("Target contact must not exceed 255 characters.");
    }
}

public class AcceptInvitationValidator : AbstractValidator<AcceptInvitationRequestDto>
{
    public AcceptInvitationValidator()
    {
        RuleFor(x => x.MonthlyContributionAmount)
            .GreaterThan(0).WithMessage("Monthly contribution amount must be > 0.");
    }
}
