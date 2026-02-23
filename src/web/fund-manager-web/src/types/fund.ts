// ── Fund DTOs aligned with FundManager.FundAdmin API ──

export interface Fund {
  id: string;
  name: string;
  description?: string;
  currency: string;
  monthlyInterestRate: number;
  minimumMonthlyContribution: number;
  minimumPrincipalPerRepayment: number;
  loanApprovalPolicy: string;
  maxLoanPerMember?: number;
  maxConcurrentLoans?: number;
  dissolutionPolicy?: string;
  overduePenaltyType: string;
  overduePenaltyValue: number;
  contributionDayOfMonth: number;
  gracePeriodDays: number;
  state: FundState;
  createdAt: string;
  updatedAt: string;
}

export type FundState = 'Draft' | 'Active' | 'Dissolving' | 'Dissolved';

export interface CreateFundRequest {
  name: string;
  description?: string;
  monthlyInterestRate: number;
  minimumMonthlyContribution: number;
  minimumPrincipalPerRepayment: number;
  currency?: string;
  loanApprovalPolicy?: string;
  maxLoanPerMember?: number;
  maxConcurrentLoans?: number;
  dissolutionPolicy?: string;
  overduePenaltyType?: string;
  overduePenaltyValue: number;
  contributionDayOfMonth: number;
  gracePeriodDays: number;
}

export interface UpdateFundRequest {
  description?: string;
  // Config fields — only applied when fund is in Draft state
  name?: string;
  monthlyInterestRate?: number;
  minimumMonthlyContribution?: number;
  minimumPrincipalPerRepayment?: number;
  currency?: string;
  loanApprovalPolicy?: string;
  maxLoanPerMember?: number | null;
  clearMaxLoanPerMember?: boolean;
  maxConcurrentLoans?: number | null;
  clearMaxConcurrentLoans?: boolean;
  dissolutionPolicy?: string;
  overduePenaltyType?: string;
  overduePenaltyValue?: number;
  contributionDayOfMonth?: number;
  gracePeriodDays?: number;
}

export interface FundDashboard {
  fundId: string;
  fundName: string;
  state: string;
  totalBalance: number;
  memberCount: number;
  activeLoansCount: number;
  pendingApprovalsCount: number;
  overdueContributionsCount: number;
  overdueRepaymentsCount: number;
  thisMonthContributionsCollected: number;
  thisMonthContributionsDue: number;
}

export interface MemberSummary {
  userId: string;
  userName: string;
  role: string;
  monthlyContributionAmount: number;
  joinDate: string;
  isActive: boolean;
}

export interface PaginatedResponse<T> {
  items: T[];
  totalCount: number;
  page: number;
  pageSize: number;
}
