export interface DissolutionSettlement {
  id: string;
  fundId: string;
  totalInterestPool: number;
  totalContributionsCollected: number;
  status: DissolutionStatusType;
  settlementDate?: string;
  confirmedBy?: string;
  createdAt: string;
}

export type DissolutionStatusType = 'Calculating' | 'Reviewed' | 'Confirmed';

export interface DissolutionLineItem {
  userId: string;
  userName?: string;
  monthlyContributionAmount?: number;
  totalPaidContributions: number;
  interestShare: number;
  outstandingLoanPrincipal: number;
  unpaidInterest: number;
  unpaidDues: number;
  grossPayout: number;
  netPayout: number;
}

export interface DissolutionBlocker {
  userId: string;
  userName?: string;
  netPayout: number;
  outstandingAmount: number;
}

export interface SettlementDetail {
  settlement: DissolutionSettlement;
  lineItems: DissolutionLineItem[];
  canConfirm: boolean;
  blockers: DissolutionBlocker[];
}
