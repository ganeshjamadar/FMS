using FundManager.BuildingBlocks.Domain;

namespace FundManager.FundAdmin.Domain.Entities;

/// <summary>
/// Fund lifecycle states: Draft → Active → Dissolving → Dissolved.
/// </summary>
public enum FundState
{
    Draft,
    Active,
    Dissolving,
    Dissolved
}

/// <summary>
/// A Fund is the core aggregate — holds all configuration for contributions, lending, and dissolution.
/// All config fields (except Description) are immutable after activation (FR-011).
/// While in Draft state, Fund Admins may update any configuration field.
/// </summary>
public class Fund : AggregateRoot
{
    public string Name { get; private set; } = string.Empty;
    public string? Description { get; private set; }
    public string Currency { get; private set; } = "INR";
    public decimal MonthlyInterestRate { get; private set; }
    public decimal MinimumMonthlyContribution { get; private set; }
    public decimal MinimumPrincipalPerRepayment { get; private set; } = 1000.00m;
    public string LoanApprovalPolicy { get; private set; } = "AdminOnly";
    public decimal? MaxLoanPerMember { get; private set; }
    public int? MaxConcurrentLoans { get; private set; }
    public string? DissolutionPolicy { get; private set; }
    public string OverduePenaltyType { get; private set; } = "None";
    public decimal OverduePenaltyValue { get; private set; } = 0.00m;
    public int ContributionDayOfMonth { get; private set; } = 1;
    public int GracePeriodDays { get; private set; } = 5;
    public FundState State { get; private set; } = FundState.Draft;

    // Navigation
    private readonly List<FundRoleAssignment> _roleAssignments = [];
    public IReadOnlyList<FundRoleAssignment> RoleAssignments => _roleAssignments.AsReadOnly();

    private readonly List<MemberContributionPlan> _memberPlans = [];
    public IReadOnlyList<MemberContributionPlan> MemberPlans => _memberPlans.AsReadOnly();

    private Fund() { } // EF Core

    /// <summary>
    /// Create a new fund in Draft state. All config fields set at creation and immutable afterward (FR-011).
    /// </summary>
    public static Fund Create(
        string name,
        decimal monthlyInterestRate,
        decimal minimumMonthlyContribution,
        decimal minimumPrincipalPerRepayment = 1000.00m,
        string? description = null,
        string currency = "INR",
        string loanApprovalPolicy = "AdminOnly",
        decimal? maxLoanPerMember = null,
        int? maxConcurrentLoans = null,
        string? dissolutionPolicy = null,
        string overduePenaltyType = "None",
        decimal overduePenaltyValue = 0.00m,
        int contributionDayOfMonth = 1,
        int gracePeriodDays = 5)
    {
        // Validation
        if (monthlyInterestRate <= 0 || monthlyInterestRate > 1.0m)
            throw new ArgumentException("Monthly interest rate must be > 0 and ≤ 1.0 (100%).");
        if (minimumMonthlyContribution <= 0)
            throw new ArgumentException("Minimum monthly contribution must be > 0.");
        if (minimumPrincipalPerRepayment <= 0)
            throw new ArgumentException("Minimum principal per repayment must be > 0.");
        if (contributionDayOfMonth < 1 || contributionDayOfMonth > 28)
            throw new ArgumentException("Contribution day must be between 1 and 28.");
        if (gracePeriodDays < 0)
            throw new ArgumentException("Grace period days must be ≥ 0.");

        return new Fund
        {
            Id = Guid.NewGuid(),
            Name = name,
            Description = description,
            Currency = currency,
            MonthlyInterestRate = monthlyInterestRate,
            MinimumMonthlyContribution = minimumMonthlyContribution,
            MinimumPrincipalPerRepayment = minimumPrincipalPerRepayment,
            LoanApprovalPolicy = loanApprovalPolicy,
            MaxLoanPerMember = maxLoanPerMember,
            MaxConcurrentLoans = maxConcurrentLoans,
            DissolutionPolicy = dissolutionPolicy,
            OverduePenaltyType = overduePenaltyType,
            OverduePenaltyValue = overduePenaltyValue,
            ContributionDayOfMonth = contributionDayOfMonth,
            GracePeriodDays = gracePeriodDays,
            State = FundState.Draft,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
    }

    /// <summary>
    /// Update description — allowed in any state.
    /// </summary>
    public void UpdateDescription(string? description)
    {
        Description = description;
        SetUpdated();
    }

    /// <summary>
    /// Update fund configuration fields. Only allowed while fund is in Draft state (FR-011).
    /// After activation, all config fields (except Description) become immutable.
    /// </summary>
    public Result UpdateConfiguration(
        string? name = null,
        decimal? monthlyInterestRate = null,
        decimal? minimumMonthlyContribution = null,
        decimal? minimumPrincipalPerRepayment = null,
        string? currency = null,
        string? loanApprovalPolicy = null,
        decimal? maxLoanPerMember = null,
        bool clearMaxLoanPerMember = false,
        int? maxConcurrentLoans = null,
        bool clearMaxConcurrentLoans = false,
        string? dissolutionPolicy = null,
        string? overduePenaltyType = null,
        decimal? overduePenaltyValue = null,
        int? contributionDayOfMonth = null,
        int? gracePeriodDays = null)
    {
        if (State != FundState.Draft)
            return Result.Failure("Fund configuration can only be updated while in Draft state.", "INVALID_STATE");

        if (name is not null)
        {
            if (string.IsNullOrWhiteSpace(name))
                return Result.Failure("Fund name cannot be empty.", "VALIDATION_ERROR");
            Name = name;
        }

        if (monthlyInterestRate.HasValue)
        {
            if (monthlyInterestRate.Value <= 0 || monthlyInterestRate.Value > 1.0m)
                return Result.Failure("Monthly interest rate must be > 0 and ≤ 1.0 (100%).", "VALIDATION_ERROR");
            MonthlyInterestRate = monthlyInterestRate.Value;
        }

        if (minimumMonthlyContribution.HasValue)
        {
            if (minimumMonthlyContribution.Value <= 0)
                return Result.Failure("Minimum monthly contribution must be > 0.", "VALIDATION_ERROR");
            MinimumMonthlyContribution = minimumMonthlyContribution.Value;
        }

        if (minimumPrincipalPerRepayment.HasValue)
        {
            if (minimumPrincipalPerRepayment.Value <= 0)
                return Result.Failure("Minimum principal per repayment must be > 0.", "VALIDATION_ERROR");
            MinimumPrincipalPerRepayment = minimumPrincipalPerRepayment.Value;
        }

        if (currency is not null)
            Currency = currency;

        if (loanApprovalPolicy is not null)
        {
            if (loanApprovalPolicy is not "AdminOnly" and not "AdminWithVoting")
                return Result.Failure("Loan approval policy must be 'AdminOnly' or 'AdminWithVoting'.", "VALIDATION_ERROR");
            LoanApprovalPolicy = loanApprovalPolicy;
        }

        if (clearMaxLoanPerMember)
            MaxLoanPerMember = null;
        else if (maxLoanPerMember.HasValue)
        {
            if (maxLoanPerMember.Value <= 0)
                return Result.Failure("Max loan per member must be > 0.", "VALIDATION_ERROR");
            MaxLoanPerMember = maxLoanPerMember.Value;
        }

        if (clearMaxConcurrentLoans)
            MaxConcurrentLoans = null;
        else if (maxConcurrentLoans.HasValue)
        {
            if (maxConcurrentLoans.Value <= 0)
                return Result.Failure("Max concurrent loans must be > 0.", "VALIDATION_ERROR");
            MaxConcurrentLoans = maxConcurrentLoans.Value;
        }

        if (dissolutionPolicy is not null)
            DissolutionPolicy = dissolutionPolicy;

        if (overduePenaltyType is not null)
        {
            if (overduePenaltyType is not "None" and not "Flat" and not "Percentage")
                return Result.Failure("Overdue penalty type must be 'None', 'Flat', or 'Percentage'.", "VALIDATION_ERROR");
            OverduePenaltyType = overduePenaltyType;
        }

        if (overduePenaltyValue.HasValue)
        {
            if (overduePenaltyValue.Value < 0)
                return Result.Failure("Overdue penalty value must be ≥ 0.", "VALIDATION_ERROR");
            OverduePenaltyValue = overduePenaltyValue.Value;
        }

        if (contributionDayOfMonth.HasValue)
        {
            if (contributionDayOfMonth.Value < 1 || contributionDayOfMonth.Value > 28)
                return Result.Failure("Contribution day must be between 1 and 28.", "VALIDATION_ERROR");
            ContributionDayOfMonth = contributionDayOfMonth.Value;
        }

        if (gracePeriodDays.HasValue)
        {
            if (gracePeriodDays.Value < 0)
                return Result.Failure("Grace period days must be ≥ 0.", "VALIDATION_ERROR");
            GracePeriodDays = gracePeriodDays.Value;
        }

        SetUpdated();
        return Result.Success();
    }

    /// <summary>
    /// Transition Draft → Active. Requires at least one Admin assigned (FR-015).
    /// </summary>
    public Result Activate()
    {
        if (State != FundState.Draft)
            return Result.Failure("Fund can only be activated from Draft state.", "INVALID_STATE");

        if (!_roleAssignments.Any(r => r.Role == "Admin"))
            return Result.Failure("Fund must have at least one Admin before activation (FR-015).", "NO_ADMIN");

        State = FundState.Active;
        SetUpdated();
        return Result.Success();
    }

    /// <summary>
    /// Transition Active → Dissolving. Blocks new members, loans, contributions (FR-081).
    /// </summary>
    public Result InitiateDissolution()
    {
        if (State != FundState.Active)
            return Result.Failure("Fund can only be dissolved from Active state.", "INVALID_STATE");

        State = FundState.Dissolving;
        SetUpdated();
        return Result.Success();
    }

    /// <summary>
    /// Transition Dissolving → Dissolved (terminal state, read-only).
    /// </summary>
    public Result ConfirmDissolution()
    {
        if (State != FundState.Dissolving)
            return Result.Failure("Fund can only be dissolved from Dissolving state.", "INVALID_STATE");

        State = FundState.Dissolved;
        SetUpdated();
        return Result.Success();
    }

    /// <summary>
    /// Assign a role to a user in this fund.
    /// </summary>
    public Result<FundRoleAssignment> AssignRole(Guid userId, string role, Guid assignedBy)
    {
        if (_roleAssignments.Any(r => r.UserId == userId))
            return Result<FundRoleAssignment>.Failure("User already has a role in this fund.", "DUPLICATE_ROLE");

        var assignment = FundRoleAssignment.Create(userId, Id, role, assignedBy);
        _roleAssignments.Add(assignment);
        SetUpdated();

        return Result<FundRoleAssignment>.Success(assignment);
    }

    /// <summary>
    /// Change a user's role. Cannot demote the last Admin (FR-015).
    /// </summary>
    public Result ChangeRole(Guid userId, string newRole)
    {
        var assignment = _roleAssignments.FirstOrDefault(r => r.UserId == userId);
        if (assignment is null)
            return Result.Failure("User not found in this fund.", "USER_NOT_FOUND");

        // Prevent demoting last Admin
        if (assignment.Role == "Admin" && newRole != "Admin")
        {
            var adminCount = _roleAssignments.Count(r => r.Role == "Admin");
            if (adminCount <= 1)
                return Result.Failure("Cannot demote the last Admin (FR-015).", "LAST_ADMIN");
        }

        assignment.ChangeRole(newRole);
        SetUpdated();
        return Result.Success();
    }
}
