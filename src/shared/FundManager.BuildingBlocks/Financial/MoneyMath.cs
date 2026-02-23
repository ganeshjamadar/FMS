namespace FundManager.BuildingBlocks.Financial;

/// <summary>
/// Deterministic monetary calculations using banker's rounding.
/// All financial computations in the system MUST use these methods.
/// Constitution Principle II: Financial Calculations Must Be Deterministic and Explainable.
/// </summary>
public static class MoneyMath
{
    /// <summary>
    /// Round a monetary value to 2 decimal places using banker's rounding (MidpointRounding.ToEven).
    /// This is the ONLY rounding method used in the system per spec.md assumption A-007.
    /// </summary>
    public static decimal Round(decimal value) =>
        Math.Round(value, 2, MidpointRounding.ToEven);

    /// <summary>
    /// Calculate reducing-balance interest for a single month.
    /// Formula: interest = Round(outstandingPrincipal × monthlyRate)
    /// Per FR-060.
    /// </summary>
    public static decimal CalculateMonthlyInterest(decimal outstandingPrincipal, decimal monthlyRate) =>
        Round(outstandingPrincipal * monthlyRate);

    /// <summary>
    /// Calculate the principal portion of a repayment.
    /// Formula: principalDue = MIN(MAX(minimumPrincipal, scheduledInstallment - interestDue), outstandingPrincipal)
    /// Per FR-061, FR-062, FR-063.
    /// </summary>
    public static decimal CalculatePrincipalDue(
        decimal outstandingPrincipal,
        decimal minimumPrincipal,
        decimal scheduledInstallment,
        decimal interestDue)
    {
        var principalFromSchedule = scheduledInstallment - interestDue;
        var principalDue = Math.Max(minimumPrincipal, principalFromSchedule);
        return Math.Min(Round(principalDue), outstandingPrincipal);
    }

    /// <summary>
    /// Calculate the total monthly repayment due.
    /// </summary>
    public static decimal CalculateTotalDue(decimal interestDue, decimal principalDue) =>
        Round(interestDue + principalDue);

    /// <summary>
    /// Apply a payment: interest first, then principal, then excess to principal.
    /// Per FR-068 (repayment allocation order: interest → principal → excess).
    /// Returns (interestPaid, principalPaid, excessApplied, remainingBalance).
    /// </summary>
    public static (decimal InterestPaid, decimal PrincipalPaid, decimal ExcessApplied, decimal RemainingBalance)
        ApplyPayment(decimal paymentAmount, decimal interestDue, decimal principalDue, decimal outstandingPrincipal)
    {
        var interestPaid = Math.Min(paymentAmount, interestDue);
        var afterInterest = paymentAmount - interestPaid;

        var principalPaid = Math.Min(afterInterest, principalDue);
        var afterPrincipal = afterInterest - principalPaid;

        // Excess goes to principal reduction
        var excessApplied = Math.Min(afterPrincipal, outstandingPrincipal - principalPaid);

        var totalPrincipalReduction = principalPaid + excessApplied;
        var remainingBalance = Round(outstandingPrincipal - totalPrincipalReduction);

        return (Round(interestPaid), Round(principalPaid), Round(excessApplied), remainingBalance);
    }

    /// <summary>
    /// Validate that a decimal value is a valid monetary amount (non-negative, max 2 decimal places).
    /// </summary>
    public static bool IsValidMonetaryAmount(decimal value) =>
        value >= 0 && value == Math.Round(value, 2);

    /// <summary>
    /// Validate that a rate value is within acceptable bounds (0 to 1.0, max 6 decimal places).
    /// </summary>
    public static bool IsValidRate(decimal rate) =>
        rate > 0 && rate <= 1.0m && rate == Math.Round(rate, 6);
}
