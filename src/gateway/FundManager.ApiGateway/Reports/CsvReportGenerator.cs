using System.Text;
using FundManager.ApiGateway.Services;

namespace FundManager.ApiGateway.Reports;

/// <summary>
/// Generates CSV exports for all report types.
/// </summary>
public class CsvReportGenerator
{
    public string GenerateContributionReport(ContributionReport report)
    {
        var sb = new StringBuilder();
        sb.AppendLine("Fund Contribution Summary Report");
        sb.AppendLine($"Fund: {report.FundName} ({report.FundId})");
        sb.AppendLine($"Period: {report.FromMonth} to {report.ToMonth}");
        sb.AppendLine($"Total Due: {report.TotalDue:F2}");
        sb.AppendLine($"Total Collected: {report.TotalCollected:F2}");
        sb.AppendLine($"Total Outstanding: {report.TotalOutstanding:F2}");
        sb.AppendLine();
        sb.AppendLine("UserId,Name,MonthYear,AmountDue,AmountPaid,Status");

        foreach (var member in report.Members)
        {
            foreach (var month in member.Months)
            {
                sb.AppendLine($"{member.UserId},{EscapeCsv(member.Name)},{month.MonthYear},{month.AmountDue:F2},{month.AmountPaid:F2},{month.Status}");
            }
        }

        return sb.ToString();
    }

    public string GenerateLoanPortfolioReport(LoanPortfolioReport report)
    {
        var sb = new StringBuilder();
        sb.AppendLine("Loan Portfolio Report");
        sb.AppendLine($"Fund: {report.FundId}");
        sb.AppendLine($"Total Active Loans: {report.TotalActiveLoans}");
        sb.AppendLine($"Total Outstanding Principal: {report.TotalOutstandingPrincipal:F2}");
        sb.AppendLine();
        sb.AppendLine("LoanId,BorrowerName,PrincipalAmount,OutstandingPrincipal,InterestEarned,MonthlyInterestRate,Status,DisbursementDate");

        foreach (var loan in report.Loans)
        {
            sb.AppendLine(string.Join(",",
                loan.LoanId,
                EscapeCsv(loan.BorrowerName),
                loan.PrincipalAmount.ToString("F2"),
                loan.OutstandingPrincipal.ToString("F2"),
                loan.InterestEarned.ToString("F2"),
                loan.MonthlyInterestRate.ToString("F6"),
                loan.Status,
                loan.DisbursementDate?.ToString("yyyy-MM-dd") ?? ""));
        }

        return sb.ToString();
    }

    public string GenerateInterestEarningsReport(InterestEarningsReport report)
    {
        var sb = new StringBuilder();
        sb.AppendLine("Interest Earnings Report");
        sb.AppendLine($"Fund: {report.FundId}");
        sb.AppendLine($"Total Interest Earned: {report.TotalInterestEarned:F2}");
        sb.AppendLine();
        sb.AppendLine("MonthYear,InterestEarned,LoanCount");

        foreach (var month in report.Months)
        {
            sb.AppendLine($"{month.MonthYear},{month.InterestEarned:F2},{month.LoanCount}");
        }

        return sb.ToString();
    }

    public string GenerateBalanceSheet(BalanceSheet report)
    {
        var sb = new StringBuilder();
        sb.AppendLine("Fund Balance Sheet");
        sb.AppendLine($"Fund: {report.FundName} ({report.FundId})");
        sb.AppendLine($"Period: {report.FromMonth} to {report.ToMonth}");
        sb.AppendLine();
        sb.AppendLine("Item,Amount");
        sb.AppendLine($"Opening Balance,{report.OpeningBalance:F2}");
        sb.AppendLine($"Contributions Received,{report.ContributionsReceived:F2}");
        sb.AppendLine($"Disbursements,{report.Disbursements:F2}");
        sb.AppendLine($"Interest Earned,{report.InterestEarned:F2}");
        sb.AppendLine($"Repayments Received,{report.RepaymentsReceived:F2}");
        sb.AppendLine($"Penalties,{report.Penalties:F2}");
        sb.AppendLine($"Closing Balance,{report.ClosingBalance:F2}");

        return sb.ToString();
    }

    public string GenerateMemberStatement(MemberStatement report)
    {
        var sb = new StringBuilder();
        sb.AppendLine("Member Statement");
        sb.AppendLine($"Member: {report.UserName} ({report.UserId})");
        sb.AppendLine($"Fund: {report.FundName} ({report.FundId})");
        sb.AppendLine($"Monthly Contribution: {report.MonthlyContributionAmount:F2}");
        sb.AppendLine($"Total Contributions Paid: {report.TotalContributionsPaid:F2}");
        sb.AppendLine();

        sb.AppendLine("--- Contribution History ---");
        sb.AppendLine("MonthYear,AmountDue,AmountPaid,Status");
        foreach (var c in report.ContributionHistory)
        {
            sb.AppendLine($"{c.MonthYear},{c.AmountDue:F2},{c.AmountPaid:F2},{c.Status}");
        }

        sb.AppendLine();
        sb.AppendLine("--- Loan History ---");
        sb.AppendLine("LoanId,PrincipalAmount,OutstandingPrincipal,TotalInterestPaid,Status");
        foreach (var l in report.LoanHistory)
        {
            sb.AppendLine($"{l.LoanId},{l.PrincipalAmount:F2},{l.OutstandingPrincipal:F2},{l.TotalInterestPaid:F2},{l.Status}");
        }

        if (report.ProjectedDissolutionPayout is not null)
        {
            sb.AppendLine();
            sb.AppendLine("--- Projected Dissolution Payout ---");
            sb.AppendLine($"Interest Share: {report.ProjectedDissolutionPayout.InterestShare:F2}");
            sb.AppendLine($"Gross Payout: {report.ProjectedDissolutionPayout.GrossPayout:F2}");
            sb.AppendLine($"Deductions: {report.ProjectedDissolutionPayout.Deductions:F2}");
            sb.AppendLine($"Net Payout: {report.ProjectedDissolutionPayout.NetPayout:F2}");
        }

        return sb.ToString();
    }

    private static string EscapeCsv(string value) =>
        value.Contains(',') || value.Contains('"') || value.Contains('\n')
            ? $"\"{value.Replace("\"", "\"\"")}\""
            : value;
}
