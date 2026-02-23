export type ContributionDueStatus = 'Pending' | 'Paid' | 'Partial' | 'Late' | 'Missed';

export interface ContributionDue {
  id: string;
  fundId: string;
  userId: string;
  monthYear: number;
  amountDue: number;
  amountPaid: number;
  remainingBalance: number;
  status: ContributionDueStatus;
  dueDate: string;
  paidDate: string | null;
  version: string;
}

export interface TransactionEntry {
  id: string;
  fundId: string;
  userId: string;
  type: string;
  amount: number;
  description: string | null;
  referenceEntityType: string | null;
  referenceEntityId: string | null;
  recordedBy: string;
  createdAt: string;
}

export interface ContributionSummary {
  fundId: string;
  monthYear: number;
  totalDue: number;
  totalCollected: number;
  totalOutstanding: number;
  paidCount: number;
  partialCount: number;
  pendingCount: number;
  lateCount: number;
  missedCount: number;
}

export interface RecordPaymentRequest {
  dueId: string;
  amount: number;
  description?: string;
}

export interface PaymentResult {
  transactionId: string;
  dueId: string;
  amountPaid: number;
  remainingBalance: number;
  newStatus: ContributionDueStatus;
}

export interface PaginatedResponse<T> {
  items: T[];
  totalCount: number;
  page: number;
  pageSize: number;
}
