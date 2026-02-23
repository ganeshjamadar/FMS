using System.Text;
using FundManager.ApiGateway.Services;

namespace FundManager.ApiGateway.Reports;

/// <summary>
/// Generates PDF reports for all report types.
/// Uses a simple text-based layout. For production, integrate QuestPDF for proper PDF generation.
/// </summary>
public class PdfReportGenerator
{
    public byte[] GenerateContributionReport(ContributionReport report)
    {
        var sb = new StringBuilder();
        sb.AppendLine("=== FUND CONTRIBUTION SUMMARY REPORT ===");
        sb.AppendLine();
        sb.AppendLine(string.Format("{0,-20} {1}", "Fund:", $"{report.FundName} ({report.FundId})"));
        sb.AppendLine(string.Format("{0,-20} {1}", "Period:", $"{report.FromMonth} to {report.ToMonth}"));
        sb.AppendLine(string.Format("{0,-20} {1}", "Total Due:", report.TotalDue.ToString("N2")));
        sb.AppendLine(string.Format("{0,-20} {1}", "Total Collected:", report.TotalCollected.ToString("N2")));
        sb.AppendLine(string.Format("{0,-20} {1}", "Total Outstanding:", report.TotalOutstanding.ToString("N2")));
        sb.AppendLine();

        sb.AppendLine(string.Format("{0,-38} {1,-20} {2,12} {3,12} {4,12} {5,-10}",
            "UserId", "Name", "Month", "Due", "Paid", "Status"));
        sb.AppendLine(new string('-', 110));

        foreach (var member in report.Members)
        {
            foreach (var month in member.Months)
            {
                sb.AppendLine(string.Format("{0,-38} {1,-20} {2,12} {3,12:N2} {4,12:N2} {5,-10}",
                    member.UserId, member.Name, month.MonthYear,
                    month.AmountDue, month.AmountPaid, month.Status));
            }
        }

        return Encoding.UTF8.GetBytes(sb.ToString());
    }

    public byte[] GenerateLoanPortfolioReport(LoanPortfolioReport report)
    {
        var sb = new StringBuilder();
        sb.AppendLine("=== LOAN PORTFOLIO REPORT ===");
        sb.AppendLine();
        sb.AppendLine(string.Format("{0,-25} {1}", "Fund:", report.FundId));
        sb.AppendLine(string.Format("{0,-25} {1}", "Active Loans:", report.TotalActiveLoans));
        sb.AppendLine(string.Format("{0,-25} {1}", "Outstanding Principal:", report.TotalOutstandingPrincipal.ToString("N2")));
        sb.AppendLine();

        sb.AppendLine(string.Format("{0,-38} {1,-15} {2,15} {3,15} {4,10} {5,-10} {6,-12}",
            "LoanId", "Borrower", "Principal", "Outstanding", "Rate", "Status", "Disbursed"));
        sb.AppendLine(new string('-', 120));

        foreach (var loan in report.Loans)
        {
            sb.AppendLine(string.Format("{0,-38} {1,-15} {2,15:N2} {3,15:N2} {4,10:P4} {5,-10} {6,-12}",
                loan.LoanId, loan.BorrowerName, loan.PrincipalAmount,
                loan.OutstandingPrincipal, loan.MonthlyInterestRate,
                loan.Status, loan.DisbursementDate?.ToString("yyyy-MM-dd") ?? "N/A"));
        }

        return Encoding.UTF8.GetBytes(sb.ToString());
    }

    public byte[] GenerateInterestEarningsReport(InterestEarningsReport report)
    {
        var sb = new StringBuilder();
        sb.AppendLine("=== INTEREST EARNINGS REPORT ===");
        sb.AppendLine();
        sb.AppendLine(string.Format("{0,-25} {1}", "Fund:", report.FundId));
        sb.AppendLine(string.Format("{0,-25} {1}", "Total Interest Earned:", report.TotalInterestEarned.ToString("N2")));
        sb.AppendLine();

        sb.AppendLine(string.Format("{0,-12} {1,15} {2,10}", "Month", "Interest", "Loans"));
        sb.AppendLine(new string('-', 40));

        foreach (var month in report.Months)
        {
            sb.AppendLine(string.Format("{0,-12} {1,15:N2} {2,10}",
                month.MonthYear, month.InterestEarned, month.LoanCount));
        }

        return Encoding.UTF8.GetBytes(sb.ToString());
    }

    public byte[] GenerateBalanceSheet(BalanceSheet report)
    {
        var sb = new StringBuilder();
        sb.AppendLine("=== FUND BALANCE SHEET ===");
        sb.AppendLine();
        sb.AppendLine(string.Format("{0,-25} {1}", "Fund:", $"{report.FundName} ({report.FundId})"));
        sb.AppendLine(string.Format("{0,-25} {1}", "Period:", $"{report.FromMonth} to {report.ToMonth}"));
        sb.AppendLine();

        sb.AppendLine(string.Format("{0,-30} {1,15}", "Item", "Amount"));
        sb.AppendLine(new string('-', 47));
        sb.AppendLine(string.Format("{0,-30} {1,15:N2}", "Opening Balance", report.OpeningBalance));
        sb.AppendLine(string.Format("{0,-30} {1,15:N2}", "Contributions Received", report.ContributionsReceived));
        sb.AppendLine(string.Format("{0,-30} {1,15:N2}", "Disbursements", report.Disbursements));
        sb.AppendLine(string.Format("{0,-30} {1,15:N2}", "Interest Earned", report.InterestEarned));
        sb.AppendLine(string.Format("{0,-30} {1,15:N2}", "Repayments Received", report.RepaymentsReceived));
        sb.AppendLine(string.Format("{0,-30} {1,15:N2}", "Penalties", report.Penalties));
        sb.AppendLine(new string('=', 47));
        sb.AppendLine(string.Format("{0,-30} {1,15:N2}", "Closing Balance", report.ClosingBalance));

        return Encoding.UTF8.GetBytes(sb.ToString());
    }

    public byte[] GenerateMemberStatement(MemberStatement report)
    {
        var sb = new StringBuilder();
        sb.AppendLine("=== MEMBER STATEMENT ===");
        sb.AppendLine();
        sb.AppendLine(string.Format("{0,-25} {1}", "Member:", $"{report.UserName} ({report.UserId})"));
        sb.AppendLine(string.Format("{0,-25} {1}", "Fund:", $"{report.FundName} ({report.FundId})"));
        sb.AppendLine(string.Format("{0,-25} {1}", "Monthly Contribution:", report.MonthlyContributionAmount.ToString("N2")));
        sb.AppendLine(string.Format("{0,-25} {1}", "Total Paid:", report.TotalContributionsPaid.ToString("N2")));
        sb.AppendLine();

        sb.AppendLine("--- Contribution History ---");
        sb.AppendLine(string.Format("{0,-12} {1,12} {2,12} {3,-10}", "Month", "Due", "Paid", "Status"));
        sb.AppendLine(new string('-', 50));
        foreach (var c in report.ContributionHistory)
        {
            sb.AppendLine(string.Format("{0,-12} {1,12:N2} {2,12:N2} {3,-10}",
                c.MonthYear, c.AmountDue, c.AmountPaid, c.Status));
        }

        sb.AppendLine();
        sb.AppendLine("--- Loan History ---");
        sb.AppendLine(string.Format("{0,-38} {1,12} {2,12} {3,12} {4,-10}",
            "LoanId", "Principal", "Outstanding", "IntPaid", "Status"));
        sb.AppendLine(new string('-', 90));
        foreach (var l in report.LoanHistory)
        {
            sb.AppendLine(string.Format("{0,-38} {1,12:N2} {2,12:N2} {3,12:N2} {4,-10}",
                l.LoanId, l.PrincipalAmount, l.OutstandingPrincipal,
                l.TotalInterestPaid, l.Status));
        }

        if (report.ProjectedDissolutionPayout is not null)
        {
            var p = report.ProjectedDissolutionPayout;
            sb.AppendLine();
            sb.AppendLine("--- Projected Dissolution Payout ---");
            sb.AppendLine(string.Format("{0,-25} {1,15:N2}", "Interest Share:", p.InterestShare));
            sb.AppendLine(string.Format("{0,-25} {1,15:N2}", "Gross Payout:", p.GrossPayout));
            sb.AppendLine(string.Format("{0,-25} {1,15:N2}", "Deductions:", p.Deductions));
            sb.AppendLine(string.Format("{0,-25} {1,15:N2}", "Net Payout:", p.NetPayout));
        }

        return Encoding.UTF8.GetBytes(sb.ToString());
    }
}
