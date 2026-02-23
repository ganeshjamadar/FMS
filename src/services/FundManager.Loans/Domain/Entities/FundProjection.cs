using FundManager.BuildingBlocks.Domain;

namespace FundManager.Loans.Domain.Entities;

/// <summary>
/// Local projection of fund configuration maintained via FundCreated events.
/// Avoids cross-service queries when validating loan requests.
/// </summary>
public class FundProjection : Entity
{
    public Guid FundId { get; private set; }
    public decimal MonthlyInterestRate { get; private set; }
    public decimal MinimumPrincipalPerRepayment { get; private set; } = 1000.00m;
    public decimal? MaxLoanPerMember { get; private set; }
    public int? MaxConcurrentLoans { get; private set; }
    public string LoanApprovalPolicy { get; private set; } = "AdminOnly";
    public bool IsActive { get; private set; } = true;

    /// <summary>
    /// Penalty type for overdue repayments: "None", "Flat", or "Percentage".
    /// FR-072: Defaults to "None".
    /// </summary>
    public string PenaltyType { get; private set; } = "None";

    /// <summary>
    /// Penalty value — flat amount in currency units, or percentage of overdue amount.
    /// FR-072/FR-073: Only relevant when PenaltyType != "None".
    /// </summary>
    public decimal PenaltyValue { get; private set; }

    private FundProjection() { }

    public static FundProjection Create(
        Guid fundId,
        decimal monthlyInterestRate,
        decimal minimumPrincipalPerRepayment,
        decimal? maxLoanPerMember,
        int? maxConcurrentLoans,
        string loanApprovalPolicy)
    {
        return new FundProjection
        {
            FundId = fundId,
            MonthlyInterestRate = monthlyInterestRate,
            MinimumPrincipalPerRepayment = minimumPrincipalPerRepayment,
            MaxLoanPerMember = maxLoanPerMember,
            MaxConcurrentLoans = maxConcurrentLoans,
            LoanApprovalPolicy = loanApprovalPolicy,
        };
    }

    public void Deactivate()
    {
        IsActive = false;
        SetUpdated();
    }
}
