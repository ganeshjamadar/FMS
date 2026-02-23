using System.Text;
using FundManager.Dissolution.Domain.Entities;

namespace FundManager.Dissolution.Domain.Services;

/// <summary>
/// Generates settlement reports in PDF and CSV formats (FR-086).
/// PDF generation is a placeholder — QuestPDF will be integrated when the package is added.
/// CSV is generated directly.
/// </summary>
public class SettlementReportGenerator
{
    /// <summary>
    /// Generate a CSV settlement report.
    /// </summary>
    public string GenerateCsv(DissolutionSettlement settlement)
    {
        var sb = new StringBuilder();

        // Header
        sb.AppendLine("Settlement Report");
        sb.AppendLine($"Fund ID,{settlement.FundId}");
        sb.AppendLine($"Status,{settlement.Status}");
        sb.AppendLine($"Total Interest Pool,{settlement.TotalInterestPool:F2}");
        sb.AppendLine($"Total Contributions Collected,{settlement.TotalContributionsCollected:F2}");
        sb.AppendLine($"Settlement Date,{settlement.SettlementDate?.ToString("yyyy-MM-dd") ?? "N/A"}");
        sb.AppendLine();

        // Line items
        sb.AppendLine("UserId,TotalPaidContributions,InterestShare,OutstandingLoanPrincipal,UnpaidInterest,UnpaidDues,GrossPayout,NetPayout");

        foreach (var li in settlement.LineItems)
        {
            sb.AppendLine(string.Join(",",
                li.UserId,
                li.TotalPaidContributions.ToString("F2"),
                li.InterestShare.ToString("F2"),
                li.OutstandingLoanPrincipal.ToString("F2"),
                li.UnpaidInterest.ToString("F2"),
                li.UnpaidDues.ToString("F2"),
                li.GrossPayout.ToString("F2"),
                li.NetPayout.ToString("F2")));
        }

        return sb.ToString();
    }

    /// <summary>
    /// Generate a PDF settlement report.
    /// Returns a byte array of the PDF content.
    /// NOTE: This is a basic implementation. For production, integrate QuestPDF.
    /// </summary>
    public byte[] GeneratePdf(DissolutionSettlement settlement)
    {
        // Basic PDF-like text output as placeholder
        // In production, use QuestPDF:
        //   Document.Create(container => { ... }).GeneratePdf();
        var content = new StringBuilder();
        content.AppendLine("SETTLEMENT REPORT");
        content.AppendLine($"Fund: {settlement.FundId}");
        content.AppendLine($"Status: {settlement.Status}");
        content.AppendLine($"Interest Pool: {settlement.TotalInterestPool:C}");
        content.AppendLine($"Total Contributions: {settlement.TotalContributionsCollected:C}");
        content.AppendLine($"Date: {settlement.SettlementDate?.ToString("yyyy-MM-dd") ?? "Pending"}");
        content.AppendLine();
        content.AppendLine("Member Breakdown:");
        content.AppendLine(new string('-', 80));
        content.AppendLine(string.Format("{0,-38} {1,12} {2,12}", "UserId", "Gross", "Net"));
        content.AppendLine(new string('-', 80));

        foreach (var li in settlement.LineItems)
        {
            content.AppendLine($"{li.UserId,-38} {li.GrossPayout,12:F2} {li.NetPayout,12:F2}");
        }

        content.AppendLine(new string('-', 80));

        // Return as UTF-8 bytes (placeholder — real PDF binary from QuestPDF)
        return Encoding.UTF8.GetBytes(content.ToString());
    }
}
