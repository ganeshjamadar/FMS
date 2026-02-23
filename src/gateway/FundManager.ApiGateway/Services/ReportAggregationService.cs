using System.Net.Http.Json;
using System.Text.Json;

namespace FundManager.ApiGateway.Services;

/// <summary>
/// Aggregates report data from downstream microservices (Contributions, Loans, Dissolution, FundAdmin).
/// Used by the Reports API endpoints in the gateway.
/// </summary>
public class ReportAggregationService
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<ReportAggregationService> _logger;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public ReportAggregationService(IHttpClientFactory httpClientFactory, ILogger<ReportAggregationService> logger)
    {
        _httpClientFactory = httpClientFactory;
        _logger = logger;
    }

    // ── Contribution Summary Report ─────────────────

    public async Task<ContributionReport> GetContributionReportAsync(
        Guid fundId, int fromMonth, int toMonth, string? authHeader, CancellationToken ct)
    {
        var fundInfo = await GetFundInfoAsync(fundId, authHeader, ct);

        // Fetch dues for each month in range
        var allDues = new List<ContributionDueItem>();
        for (var m = fromMonth; m <= toMonth; m++)
        {
            var monthDues = await GetContributionDuesAsync(fundId, m, authHeader, ct);
            allDues.AddRange(monthDues);
        }

        // Group by UserId → build per-member rows
        var memberGroups = allDues
            .GroupBy(d => d.UserId)
            .Select(g =>
            {
                var months = g.Select(d => new ContributionMonthEntry
                {
                    MonthYear = d.MonthYear,
                    AmountDue = d.AmountDue,
                    AmountPaid = d.AmountPaid,
                    Status = d.Status
                }).OrderBy(m => m.MonthYear).ToList();

                return new ContributionMemberRow
                {
                    UserId = g.Key,
                    Name = g.Key.ToString()[..8], // Placeholder — no user-name cross-lookup yet
                    MonthlyAmount = months.FirstOrDefault()?.AmountDue ?? 0,
                    Months = months
                };
            })
            .ToList();

        return new ContributionReport
        {
            FundId = fundId,
            FundName = fundInfo?.Name ?? fundId.ToString(),
            FromMonth = fromMonth,
            ToMonth = toMonth,
            TotalDue = allDues.Sum(d => d.AmountDue),
            TotalCollected = allDues.Sum(d => d.AmountPaid),
            TotalOutstanding = allDues.Sum(d => d.RemainingBalance),
            Members = memberGroups
        };
    }

    // ── Loan Portfolio Report ───────────────────────

    public async Task<LoanPortfolioReport> GetLoanPortfolioReportAsync(
        Guid fundId, string? authHeader, CancellationToken ct)
    {
        var loans = await GetLoansAsync(fundId, authHeader, ct);

        var activeLoans = loans.Where(l =>
            l.Status is "Active" or "Disbursed" or "Approved").ToList();

        return new LoanPortfolioReport
        {
            FundId = fundId,
            TotalActiveLoans = activeLoans.Count,
            TotalOutstandingPrincipal = activeLoans.Sum(l => l.OutstandingPrincipal),
            TotalInterestAccrued = 0, // Would require repayment aggregation
            Loans = loans.Select(l => new LoanPortfolioItem
            {
                LoanId = l.Id,
                BorrowerName = l.BorrowerId.ToString()[..8],
                PrincipalAmount = l.PrincipalAmount,
                OutstandingPrincipal = l.OutstandingPrincipal,
                InterestEarned = 0,
                MonthlyInterestRate = l.MonthlyInterestRate,
                Status = l.Status,
                DisbursementDate = l.DisbursementDate,
                NextRepaymentDue = null
            }).ToList()
        };
    }

    // ── Interest Earnings Report ────────────────────

    public async Task<InterestEarningsReport> GetInterestEarningsReportAsync(
        Guid fundId, int fromMonth, int toMonth, string? authHeader, CancellationToken ct)
    {
        // Fetch all loans, then repayments per loan to build monthly interest breakdown
        var loans = await GetLoansAsync(fundId, authHeader, ct);
        var monthlyInterest = new Dictionary<int, (decimal interest, int loanCount)>();

        foreach (var loan in loans)
        {
            var repayments = await GetRepaymentsAsync(fundId, loan.Id, authHeader, ct);
            foreach (var r in repayments.Where(r => r.MonthYear >= fromMonth && r.MonthYear <= toMonth))
            {
                if (!monthlyInterest.ContainsKey(r.MonthYear))
                    monthlyInterest[r.MonthYear] = (0, 0);

                var current = monthlyInterest[r.MonthYear];
                monthlyInterest[r.MonthYear] = (current.interest + r.InterestDue, current.loanCount + 1);
            }
        }

        var months = monthlyInterest
            .OrderBy(kv => kv.Key)
            .Select(kv => new InterestMonthEntry
            {
                MonthYear = kv.Key,
                InterestEarned = kv.Value.interest,
                LoanCount = kv.Value.loanCount
            })
            .ToList();

        return new InterestEarningsReport
        {
            FundId = fundId,
            TotalInterestEarned = months.Sum(m => m.InterestEarned),
            Months = months
        };
    }

    // ── Balance Sheet ───────────────────────────────

    public async Task<BalanceSheet> GetBalanceSheetAsync(
        Guid fundId, int fromMonth, int toMonth, string? authHeader, CancellationToken ct)
    {
        var fundInfo = await GetFundInfoAsync(fundId, authHeader, ct);

        // Parse date range from YYYYMM
        var fromDate = DateOnly.Parse($"{fromMonth / 100:D4}-{fromMonth % 100:D2}-01");
        var toDate = fromDate.AddMonths(toMonth - fromMonth + 1).AddDays(-1);

        var ledger = await GetLedgerAsync(fundId, fromDate, toDate, authHeader, ct);

        var contributions = ledger.Where(t => t.Type == "Contribution").Sum(t => t.Amount);
        var disbursements = ledger.Where(t => t.Type == "Disbursement").Sum(t => t.Amount);
        var interestEarned = ledger.Where(t => t.Type == "InterestIncome").Sum(t => t.Amount);
        var repayments = ledger.Where(t => t.Type == "Repayment").Sum(t => t.Amount);
        var penalties = ledger.Where(t => t.Type == "Penalty").Sum(t => t.Amount);

        var closingBalance = contributions - disbursements + repayments + interestEarned + penalties;

        return new BalanceSheet
        {
            FundId = fundId,
            FundName = fundInfo?.Name ?? fundId.ToString(),
            FromMonth = fromMonth,
            ToMonth = toMonth,
            OpeningBalance = 0, // Would need prior-period calculation
            ContributionsReceived = contributions,
            Disbursements = disbursements,
            InterestEarned = interestEarned,
            RepaymentsReceived = repayments,
            Penalties = penalties,
            ClosingBalance = closingBalance
        };
    }

    // ── Member Statement ────────────────────────────

    public async Task<MemberStatement> GetMemberStatementAsync(
        Guid fundId, Guid userId, string? authHeader, CancellationToken ct)
    {
        var fundInfo = await GetFundInfoAsync(fundId, authHeader, ct);

        // Get member's contribution dues
        var dues = await GetContributionDuesByUserAsync(fundId, userId, authHeader, ct);
        var contributionHistory = dues
            .OrderBy(d => d.MonthYear)
            .Select(d => new MemberContributionEntry
            {
                MonthYear = d.MonthYear,
                AmountDue = d.AmountDue,
                AmountPaid = d.AmountPaid,
                Status = d.Status
            })
            .ToList();

        // Get member's loans
        var loans = await GetLoansByBorrowerAsync(fundId, userId, authHeader, ct);
        var loanHistory = new List<MemberLoanEntry>();
        foreach (var loan in loans)
        {
            var repayments = await GetRepaymentsAsync(fundId, loan.Id, authHeader, ct);
            loanHistory.Add(new MemberLoanEntry
            {
                LoanId = loan.Id,
                PrincipalAmount = loan.PrincipalAmount,
                OutstandingPrincipal = loan.OutstandingPrincipal,
                TotalInterestPaid = repayments.Sum(r => r.AmountPaid > 0 ? r.InterestDue : 0),
                Status = loan.Status
            });
        }

        // Try to get dissolution projection
        DissolutionProjection? dissolutionProjection = null;
        try
        {
            dissolutionProjection = await GetDissolutionProjectionAsync(fundId, userId, authHeader, ct);
        }
        catch
        {
            // Dissolution may not exist yet — that's fine
        }

        return new MemberStatement
        {
            UserId = userId,
            UserName = userId.ToString()[..8],
            FundId = fundId,
            FundName = fundInfo?.Name ?? fundId.ToString(),
            MonthlyContributionAmount = dues.FirstOrDefault()?.AmountDue ?? 0,
            JoinDate = DateOnly.FromDateTime(DateTime.UtcNow), // Placeholder
            ContributionHistory = contributionHistory,
            TotalContributionsPaid = dues.Sum(d => d.AmountPaid),
            LoanHistory = loanHistory,
            ProjectedDissolutionPayout = dissolutionProjection
        };
    }

    // ── Private HTTP helpers ────────────────────────

    private async Task<FundInfoDto?> GetFundInfoAsync(Guid fundId, string? authHeader, CancellationToken ct)
    {
        try
        {
            var client = CreateClient("fundadmin", authHeader);
            return await client.GetFromJsonAsync<FundInfoDto>(
                $"/api/funds/{fundId}", JsonOptions, ct);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to fetch fund info for {FundId}", fundId);
            return null;
        }
    }

    private async Task<List<ContributionDueItem>> GetContributionDuesAsync(
        Guid fundId, int monthYear, string? authHeader, CancellationToken ct)
    {
        try
        {
            var client = CreateClient("contributions", authHeader);
            var response = await client.GetFromJsonAsync<PagedResult<ContributionDueItem>>(
                $"/api/funds/{fundId}/contributions/dues?monthYear={monthYear}&pageSize=500",
                JsonOptions, ct);
            return response?.Items ?? new();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to fetch contribution dues for {FundId} month {Month}", fundId, monthYear);
            return new();
        }
    }

    private async Task<List<ContributionDueItem>> GetContributionDuesByUserAsync(
        Guid fundId, Guid userId, string? authHeader, CancellationToken ct)
    {
        try
        {
            var client = CreateClient("contributions", authHeader);
            var response = await client.GetFromJsonAsync<PagedResult<ContributionDueItem>>(
                $"/api/funds/{fundId}/contributions/dues?userId={userId}&pageSize=500",
                JsonOptions, ct);
            return response?.Items ?? new();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to fetch dues for user {UserId}", userId);
            return new();
        }
    }

    private async Task<List<LoanItem>> GetLoansAsync(
        Guid fundId, string? authHeader, CancellationToken ct)
    {
        try
        {
            var client = CreateClient("loans", authHeader);
            var response = await client.GetFromJsonAsync<PagedResult<LoanItem>>(
                $"/api/funds/{fundId}/loans?pageSize=100",
                JsonOptions, ct);
            return response?.Items ?? new();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to fetch loans for {FundId}", fundId);
            return new();
        }
    }

    private async Task<List<LoanItem>> GetLoansByBorrowerAsync(
        Guid fundId, Guid borrowerId, string? authHeader, CancellationToken ct)
    {
        try
        {
            var client = CreateClient("loans", authHeader);
            var response = await client.GetFromJsonAsync<PagedResult<LoanItem>>(
                $"/api/funds/{fundId}/loans?borrowerId={borrowerId}&pageSize=100",
                JsonOptions, ct);
            return response?.Items ?? new();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to fetch loans for borrower {BorrowerId}", borrowerId);
            return new();
        }
    }

    private async Task<List<RepaymentItem>> GetRepaymentsAsync(
        Guid fundId, Guid loanId, string? authHeader, CancellationToken ct)
    {
        try
        {
            var client = CreateClient("loans", authHeader);
            var response = await client.GetFromJsonAsync<List<RepaymentItem>>(
                $"/api/funds/{fundId}/loans/{loanId}/repayments",
                JsonOptions, ct);
            return response ?? new();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to fetch repayments for loan {LoanId}", loanId);
            return new();
        }
    }

    private async Task<List<LedgerItem>> GetLedgerAsync(
        Guid fundId, DateOnly fromDate, DateOnly toDate, string? authHeader, CancellationToken ct)
    {
        try
        {
            var client = CreateClient("contributions", authHeader);
            var response = await client.GetFromJsonAsync<PagedResult<LedgerItem>>(
                $"/api/funds/{fundId}/contributions/ledger?fromDate={fromDate:yyyy-MM-dd}&toDate={toDate:yyyy-MM-dd}&pageSize=5000",
                JsonOptions, ct);
            return response?.Items ?? new();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to fetch ledger for {FundId}", fundId);
            return new();
        }
    }

    private async Task<DissolutionProjection?> GetDissolutionProjectionAsync(
        Guid fundId, Guid userId, string? authHeader, CancellationToken ct)
    {
        var client = CreateClient("dissolution", authHeader);
        var detail = await client.GetFromJsonAsync<DissolutionSettlementDetail>(
            $"/api/funds/{fundId}/dissolution/settlement",
            JsonOptions, ct);

        var lineItem = detail?.LineItems?.FirstOrDefault(li => li.UserId == userId);
        if (lineItem is null) return null;

        return new DissolutionProjection
        {
            InterestShare = lineItem.InterestShare,
            GrossPayout = lineItem.GrossPayout,
            Deductions = lineItem.OutstandingLoanPrincipal + lineItem.UnpaidInterest + lineItem.UnpaidDues,
            NetPayout = lineItem.NetPayout
        };
    }

    private HttpClient CreateClient(string serviceName, string? authHeader)
    {
        var client = _httpClientFactory.CreateClient(serviceName);
        if (!string.IsNullOrEmpty(authHeader))
            client.DefaultRequestHeaders.TryAddWithoutValidation("Authorization", authHeader);
        return client;
    }
}

// ── Internal DTOs for downstream service responses ──

public class PagedResult<T>
{
    public List<T> Items { get; set; } = new();
    public int TotalCount { get; set; }
}

public class FundInfoDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Currency { get; set; } = string.Empty;
    public decimal MonthlyInterestRate { get; set; }
}

public class ContributionDueItem
{
    public Guid Id { get; set; }
    public Guid FundId { get; set; }
    public Guid UserId { get; set; }
    public int MonthYear { get; set; }
    public decimal AmountDue { get; set; }
    public decimal AmountPaid { get; set; }
    public decimal RemainingBalance { get; set; }
    public string Status { get; set; } = string.Empty;
}

public class LoanItem
{
    public Guid Id { get; set; }
    public Guid FundId { get; set; }
    public Guid BorrowerId { get; set; }
    public decimal PrincipalAmount { get; set; }
    public decimal OutstandingPrincipal { get; set; }
    public decimal MonthlyInterestRate { get; set; }
    public string Status { get; set; } = string.Empty;
    public DateTime? DisbursementDate { get; set; }
}

public class RepaymentItem
{
    public Guid Id { get; set; }
    public Guid LoanId { get; set; }
    public int MonthYear { get; set; }
    public decimal InterestDue { get; set; }
    public decimal PrincipalDue { get; set; }
    public decimal TotalDue { get; set; }
    public decimal AmountPaid { get; set; }
    public string Status { get; set; } = string.Empty;
}

public class LedgerItem
{
    public Guid Id { get; set; }
    public Guid FundId { get; set; }
    public Guid UserId { get; set; }
    public string Type { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class DissolutionSettlementDetail
{
    public DissolutionSettlementInfo? Settlement { get; set; }
    public List<DissolutionLineItemInfo> LineItems { get; set; } = new();
}

public class DissolutionSettlementInfo
{
    public Guid Id { get; set; }
    public string Status { get; set; } = string.Empty;
}

public class DissolutionLineItemInfo
{
    public Guid UserId { get; set; }
    public decimal InterestShare { get; set; }
    public decimal GrossPayout { get; set; }
    public decimal OutstandingLoanPrincipal { get; set; }
    public decimal UnpaidInterest { get; set; }
    public decimal UnpaidDues { get; set; }
    public decimal NetPayout { get; set; }
}

// ── Report DTOs (returned by controller) ────────────

public class ContributionReport
{
    public Guid FundId { get; set; }
    public string FundName { get; set; } = string.Empty;
    public int FromMonth { get; set; }
    public int ToMonth { get; set; }
    public decimal TotalDue { get; set; }
    public decimal TotalCollected { get; set; }
    public decimal TotalOutstanding { get; set; }
    public List<ContributionMemberRow> Members { get; set; } = new();
}

public class ContributionMemberRow
{
    public Guid UserId { get; set; }
    public string Name { get; set; } = string.Empty;
    public decimal MonthlyAmount { get; set; }
    public List<ContributionMonthEntry> Months { get; set; } = new();
}

public class ContributionMonthEntry
{
    public int MonthYear { get; set; }
    public decimal AmountDue { get; set; }
    public decimal AmountPaid { get; set; }
    public string Status { get; set; } = string.Empty;
}

public class LoanPortfolioReport
{
    public Guid FundId { get; set; }
    public int TotalActiveLoans { get; set; }
    public decimal TotalOutstandingPrincipal { get; set; }
    public decimal TotalInterestAccrued { get; set; }
    public List<LoanPortfolioItem> Loans { get; set; } = new();
}

public class LoanPortfolioItem
{
    public Guid LoanId { get; set; }
    public string BorrowerName { get; set; } = string.Empty;
    public decimal PrincipalAmount { get; set; }
    public decimal OutstandingPrincipal { get; set; }
    public decimal InterestEarned { get; set; }
    public decimal MonthlyInterestRate { get; set; }
    public string Status { get; set; } = string.Empty;
    public DateTime? DisbursementDate { get; set; }
    public decimal? NextRepaymentDue { get; set; }
}

public class InterestEarningsReport
{
    public Guid FundId { get; set; }
    public decimal TotalInterestEarned { get; set; }
    public List<InterestMonthEntry> Months { get; set; } = new();
}

public class InterestMonthEntry
{
    public int MonthYear { get; set; }
    public decimal InterestEarned { get; set; }
    public int LoanCount { get; set; }
}

public class BalanceSheet
{
    public Guid FundId { get; set; }
    public string FundName { get; set; } = string.Empty;
    public int FromMonth { get; set; }
    public int ToMonth { get; set; }
    public decimal OpeningBalance { get; set; }
    public decimal ContributionsReceived { get; set; }
    public decimal Disbursements { get; set; }
    public decimal InterestEarned { get; set; }
    public decimal RepaymentsReceived { get; set; }
    public decimal Penalties { get; set; }
    public decimal ClosingBalance { get; set; }
}

public class MemberStatement
{
    public Guid UserId { get; set; }
    public string UserName { get; set; } = string.Empty;
    public Guid FundId { get; set; }
    public string FundName { get; set; } = string.Empty;
    public decimal MonthlyContributionAmount { get; set; }
    public DateOnly JoinDate { get; set; }
    public List<MemberContributionEntry> ContributionHistory { get; set; } = new();
    public decimal TotalContributionsPaid { get; set; }
    public List<MemberLoanEntry> LoanHistory { get; set; } = new();
    public DissolutionProjection? ProjectedDissolutionPayout { get; set; }
}

public class MemberContributionEntry
{
    public int MonthYear { get; set; }
    public decimal AmountDue { get; set; }
    public decimal AmountPaid { get; set; }
    public string Status { get; set; } = string.Empty;
}

public class MemberLoanEntry
{
    public Guid LoanId { get; set; }
    public decimal PrincipalAmount { get; set; }
    public decimal OutstandingPrincipal { get; set; }
    public decimal TotalInterestPaid { get; set; }
    public string Status { get; set; } = string.Empty;
}

public class DissolutionProjection
{
    public decimal InterestShare { get; set; }
    public decimal GrossPayout { get; set; }
    public decimal Deductions { get; set; }
    public decimal NetPayout { get; set; }
}
