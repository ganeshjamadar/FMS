namespace FundManager.Dissolution.Domain.Entities;

/// <summary>
/// Local projection of a fund member, populated by MemberJoined events.
/// </summary>
public class MemberProjection
{
    public Guid Id { get; set; }
    public Guid FundId { get; set; }
    public Guid UserId { get; set; }
    public decimal MonthlyContributionAmount { get; set; }
    public DateTime CreatedAt { get; set; }
}

/// <summary>
/// Local projection of a loan, populated by LoanDisbursed / RepaymentRecorded / LoanClosed events.
/// </summary>
public class LoanProjection
{
    public Guid Id { get; set; }
    public Guid FundId { get; set; }
    public Guid BorrowerId { get; set; }
    public decimal OutstandingPrincipal { get; set; }
    public decimal UnpaidInterest { get; set; }
    public string Status { get; set; } = "Active";
    public DateTime UpdatedAt { get; set; }
}

/// <summary>
/// Local projection of paid contributions per member per fund.
/// </summary>
public class ContributionProjection
{
    public Guid Id { get; set; }
    public Guid FundId { get; set; }
    public Guid UserId { get; set; }
    public decimal TotalPaid { get; set; }
    public decimal UnpaidAmount { get; set; }
    public DateTime UpdatedAt { get; set; }
}

/// <summary>
/// Local projection of interest income earned by the fund.
/// </summary>
public class InterestIncomeProjection
{
    public Guid Id { get; set; }
    public Guid FundId { get; set; }
    public decimal Amount { get; set; }
    public DateTime RecordedAt { get; set; }
}
