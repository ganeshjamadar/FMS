using System.Text.Json;
using System.Text.RegularExpressions;

namespace FundManager.Notifications.Infrastructure.Templates;

/// <summary>
/// Simple template engine with placeholder substitution (FR-103).
/// Templates are keyed by dot-separated names (e.g., 'contribution.due.generated').
/// Placeholders in templates use {{key}} syntax.
/// </summary>
public partial class NotificationTemplateEngine
{
    private static readonly Dictionary<string, (string Title, string Body)> Templates = new()
    {
        // ── Contribution Events ──
        ["contribution.due.generated"] = (
            "Contribution Dues Generated",
            "Contribution dues for period {{monthYear}} have been generated for your fund {{fundName}}. Total amount: {{totalAmount}}."
        ),
        ["contribution.paid"] = (
            "Contribution Payment Received",
            "Your contribution payment of {{amount}} for fund {{fundName}} has been confirmed."
        ),
        ["contribution.overdue"] = (
            "Contribution Overdue",
            "Your contribution for period {{monthYear}} in fund {{fundName}} is overdue. Please make your payment."
        ),

        // ── Loan Events ──
        ["loan.approved"] = (
            "Loan Approved",
            "Your loan request for fund {{fundName}} has been approved. Scheduled installment: {{installment}}."
        ),
        ["loan.rejected"] = (
            "Loan Rejected",
            "Your loan request for fund {{fundName}} has been rejected. Reason: {{reason}}."
        ),
        ["loan.requested"] = (
            "New Loan Request",
            "A new loan request of {{amount}} has been submitted in fund {{fundName}} by a member."
        ),

        // ── Repayment Events ──
        ["repayment.due.generated"] = (
            "Repayment Due",
            "Your repayment for period {{monthYear}} is due. Total due: {{totalDue}} (Interest: {{interestDue}}, Principal: {{principalDue}})."
        ),
        ["repayment.recorded"] = (
            "Repayment Recorded",
            "Your repayment of {{amount}} has been recorded. Remaining balance: {{remainingBalance}}."
        ),

        // ── Voting Events ──
        ["voting.started"] = (
            "Voting Session Started",
            "A voting session has started for a loan in fund {{fundName}}. Please cast your vote before {{windowEnd}}."
        ),
        ["voting.finalised"] = (
            "Voting Complete",
            "Voting for the loan in fund {{fundName}} has concluded. Result: {{result}}."
        ),

        // ── Dissolution Events ──
        ["dissolution.initiated"] = (
            "Fund Dissolution Initiated",
            "Dissolution has been initiated for fund {{fundName}}. Settlement calculations will begin shortly."
        ),
        ["dissolution.confirmed"] = (
            "Fund Dissolved",
            "Fund {{fundName}} has been dissolved. Final settlements will be processed."
        ),

        // ── Membership Events ──
        ["member.joined"] = (
            "New Member Joined",
            "A new member has joined fund {{fundName}}."
        ),
        ["invitation.sent"] = (
            "Fund Invitation",
            "You have been invited to join fund {{fundName}}. Check your invitations to accept."
        ),
    };

    /// <summary>
    /// Render a template with the given placeholders.
    /// Returns (title, body) with all {{key}} placeholders replaced.
    /// </summary>
    public (string Title, string Body) Render(string templateKey, JsonDocument placeholders)
    {
        if (!Templates.TryGetValue(templateKey, out var template))
        {
            return (templateKey, $"Notification: {templateKey}");
        }

        var values = new Dictionary<string, string>();
        foreach (var prop in placeholders.RootElement.EnumerateObject())
        {
            values[prop.Name] = prop.Value.ToString();
        }

        var title = ReplacePlaceholders(template.Title, values);
        var body = ReplacePlaceholders(template.Body, values);

        return (title, body);
    }

    /// <summary>
    /// Render a template with a pre-parsed dictionary of values.
    /// </summary>
    public (string Title, string Body) Render(string templateKey, Dictionary<string, string> placeholders)
    {
        if (!Templates.TryGetValue(templateKey, out var template))
        {
            return (templateKey, $"Notification: {templateKey}");
        }

        var title = ReplacePlaceholders(template.Title, placeholders);
        var body = ReplacePlaceholders(template.Body, placeholders);

        return (title, body);
    }

    private static string ReplacePlaceholders(string template, Dictionary<string, string> values)
    {
        return PlaceholderPattern().Replace(template, match =>
        {
            var key = match.Groups[1].Value;
            return values.TryGetValue(key, out var value) ? value : match.Value;
        });
    }

    [GeneratedRegex(@"\{\{(\w+)\}\}")]
    private static partial Regex PlaceholderPattern();
}
