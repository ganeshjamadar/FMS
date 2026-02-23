export interface Loan {
  id: string;
  fundId: string;
  borrowerId: string;
  principalAmount: number;
  outstandingPrincipal: number;
  monthlyInterestRate: number;
  scheduledInstallment: number;
  minimumPrincipal: number;
  requestedStartMonth: number;
  purpose?: string;
  status: LoanStatus;
  approvedBy?: string;
  rejectionReason?: string;
  approvalDate?: string;
  disbursementDate?: string;
  closedDate?: string;
  createdAt: string;
}

export type LoanStatus = 'PendingApproval' | 'Approved' | 'Active' | 'Closed' | 'Rejected';

export interface LoanRequest {
  principalAmount: number;
  requestedStartMonth: number;
  purpose?: string;
}

export interface ApproveLoanRequest {
  scheduledInstallment: number;
}

export interface RejectLoanRequest {
  reason: string;
}

export interface RepaymentEntry {
  id: string;
  loanId: string;
  monthYear: number;
  interestDue: number;
  principalDue: number;
  totalDue: number;
  amountPaid: number;
  status: RepaymentStatus;
  dueDate: string;
  paidDate?: string;
  version: string;
}

export type RepaymentStatus = 'Pending' | 'Paid' | 'Partial' | 'Overdue';

export interface RecordRepaymentRequest {
  amount: number;
  description?: string;
}

export interface RepaymentResult {
  transactionId: string;
  repaymentId: string;
  interestPaid: number;
  principalPaid: number;
  excessAppliedToPrincipal: number;
  newOutstandingPrincipal: number;
  repaymentStatus: RepaymentStatus;
  loanStatus: LoanStatus;
}

export interface PaginatedLoanList {
  items: Loan[];
  totalCount: number;
  page: number;
  pageSize: number;
}

// ── Voting Types ──────────────────────────────

export type VotingResultType = 'Pending' | 'Approved' | 'Rejected' | 'NoQuorum';

export interface VotingSession {
  id: string;
  loanId: string;
  votingWindowStart: string;
  votingWindowEnd: string;
  thresholdType: string;
  thresholdValue: number;
  result: VotingResultType;
  overrideUsed: boolean;
  finalisedBy?: string;
  finalisedDate?: string;
}

export interface VotingSessionDetail extends VotingSession {
  approveCount: number;
  rejectCount: number;
  totalEligible: number;
  votes: VoteSummary[];
}

export interface VoteSummary {
  voterId: string;
  voterName?: string;
  decision: 'Approve' | 'Reject';
  castAt: string;
}

export interface StartVotingRequest {
  votingWindowHours?: number;
  thresholdType?: string;
  thresholdValue?: number;
}

export interface CastVoteRequest {
  decision: 'Approve' | 'Reject';
}

export interface FinaliseVotingRequest {
  decision: 'Approve' | 'Reject';
}
