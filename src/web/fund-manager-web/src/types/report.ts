// ── Contribution Report ─────────────────────

export interface ContributionMonthEntry {
  monthYear: number;
  amountDue: number;
  amountPaid: number;
  status: string;
}

export interface ContributionMemberRow {
  userId: string;
  name: string;
  monthlyAmount: number;
  months: ContributionMonthEntry[];
}

export interface ContributionReport {
  fundId: string;
  fundName: string;
  fromMonth: number;
  toMonth: number;
  totalDue: number;
  totalCollected: number;
  totalOutstanding: number;
  members: ContributionMemberRow[];
}

// ── Loan Portfolio Report ───────────────────

export interface LoanPortfolioItem {
  loanId: string;
  borrowerName: string;
  principalAmount: number;
  outstandingPrincipal: number;
  interestEarned: number;
  monthlyInterestRate: number;
  status: string;
  disbursementDate: string | null;
  nextRepaymentDue: number | null;
}

export interface LoanPortfolioReport {
  fundId: string;
  totalActiveLoans: number;
  totalOutstandingPrincipal: number;
  totalInterestAccrued: number;
  loans: LoanPortfolioItem[];
}

// ── Interest Earnings Report ────────────────

export interface InterestMonthEntry {
  monthYear: number;
  interestEarned: number;
  loanCount: number;
}

export interface InterestEarningsReport {
  fundId: string;
  totalInterestEarned: number;
  months: InterestMonthEntry[];
}

// ── Balance Sheet ───────────────────────────

export interface BalanceSheet {
  fundId: string;
  fundName: string;
  fromMonth: number;
  toMonth: number;
  openingBalance: number;
  contributionsReceived: number;
  disbursements: number;
  interestEarned: number;
  repaymentsReceived: number;
  penalties: number;
  closingBalance: number;
}

// ── Member Statement ────────────────────────

export interface MemberContributionEntry {
  monthYear: number;
  amountDue: number;
  amountPaid: number;
  status: string;
}

export interface MemberLoanEntry {
  loanId: string;
  principalAmount: number;
  outstandingPrincipal: number;
  totalInterestPaid: number;
  status: string;
}

export interface DissolutionProjection {
  interestShare: number;
  grossPayout: number;
  deductions: number;
  netPayout: number;
}

export interface MemberStatement {
  userId: string;
  userName: string;
  fundId: string;
  fundName: string;
  monthlyContributionAmount: number;
  joinDate: string;
  contributionHistory: MemberContributionEntry[];
  totalContributionsPaid: number;
  loanHistory: MemberLoanEntry[];
  projectedDissolutionPayout: DissolutionProjection | null;
}

// ── Report type union ───────────────────────

export type ReportType =
  | 'contribution-summary'
  | 'loan-portfolio'
  | 'interest-earnings'
  | 'balance-sheet'
  | 'member-statement';

export type ExportFormat = 'json' | 'pdf' | 'csv';
