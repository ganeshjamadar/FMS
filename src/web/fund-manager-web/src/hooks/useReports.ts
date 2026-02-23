import { useQuery } from '@tanstack/react-query';
import apiClient from '@/services/apiClient';
import type {
  ContributionReport,
  LoanPortfolioReport,
  InterestEarningsReport,
  BalanceSheet,
  MemberStatement,
  ExportFormat,
} from '@/types/report';

export const reportKeys = {
  all: (fundId: string) => ['reports', fundId] as const,
  contributions: (fundId: string, fromMonth: number, toMonth: number) =>
    ['reports', fundId, 'contribution-summary', fromMonth, toMonth] as const,
  loanPortfolio: (fundId: string) =>
    ['reports', fundId, 'loan-portfolio'] as const,
  interestEarnings: (fundId: string, fromMonth: number, toMonth: number) =>
    ['reports', fundId, 'interest-earnings', fromMonth, toMonth] as const,
  balanceSheet: (fundId: string, fromMonth: number, toMonth: number) =>
    ['reports', fundId, 'balance-sheet', fromMonth, toMonth] as const,
  memberStatement: (fundId: string, userId: string) =>
    ['reports', fundId, 'member-statement', userId] as const,
};

// ── Contribution Summary ──────────────────────

export function useContributionReport(fundId: string, fromMonth: number, toMonth: number) {
  return useQuery({
    queryKey: reportKeys.contributions(fundId, fromMonth, toMonth),
    queryFn: async () => {
      const { data } = await apiClient.get<ContributionReport>(
        `/funds/${fundId}/reports/contribution-summary`,
        { params: { fromMonth, toMonth } }
      );
      return data;
    },
    enabled: !!fundId && fromMonth > 0 && toMonth > 0,
  });
}

// ── Loan Portfolio ────────────────────────────

export function useLoanPortfolioReport(fundId: string) {
  return useQuery({
    queryKey: reportKeys.loanPortfolio(fundId),
    queryFn: async () => {
      const { data } = await apiClient.get<LoanPortfolioReport>(
        `/funds/${fundId}/reports/loan-portfolio`
      );
      return data;
    },
    enabled: !!fundId,
  });
}

// ── Interest Earnings ─────────────────────────

export function useInterestEarningsReport(fundId: string, fromMonth: number, toMonth: number) {
  return useQuery({
    queryKey: reportKeys.interestEarnings(fundId, fromMonth, toMonth),
    queryFn: async () => {
      const { data } = await apiClient.get<InterestEarningsReport>(
        `/funds/${fundId}/reports/interest-earnings`,
        { params: { fromMonth, toMonth } }
      );
      return data;
    },
    enabled: !!fundId && fromMonth > 0 && toMonth > 0,
  });
}

// ── Balance Sheet ─────────────────────────────

export function useBalanceSheet(fundId: string, fromMonth: number, toMonth: number) {
  return useQuery({
    queryKey: reportKeys.balanceSheet(fundId, fromMonth, toMonth),
    queryFn: async () => {
      const { data } = await apiClient.get<BalanceSheet>(
        `/funds/${fundId}/reports/balance-sheet`,
        { params: { fromMonth, toMonth } }
      );
      return data;
    },
    enabled: !!fundId && fromMonth > 0 && toMonth > 0,
  });
}

// ── Member Statement ──────────────────────────

export function useMemberStatement(fundId: string, userId: string) {
  return useQuery({
    queryKey: reportKeys.memberStatement(fundId, userId),
    queryFn: async () => {
      const { data } = await apiClient.get<MemberStatement>(
        `/funds/${fundId}/reports/member/${userId}/statement`
      );
      return data;
    },
    enabled: !!fundId && !!userId,
  });
}

// ── Export / Download ─────────────────────────

export async function downloadReport(
  fundId: string,
  reportType: string,
  format: ExportFormat,
  params?: Record<string, string | number>
): Promise<void> {
  const queryParams = new URLSearchParams();
  queryParams.set('format', format);
  if (params) {
    Object.entries(params).forEach(([key, value]) => {
      queryParams.set(key, String(value));
    });
  }

  const url = `/funds/${fundId}/reports/${reportType}?${queryParams.toString()}`;
  const response = await apiClient.get(url, { responseType: 'blob' });
  const blob = new Blob([response.data]);
  const downloadUrl = window.URL.createObjectURL(blob);
  const link = document.createElement('a');
  link.href = downloadUrl;

  const ext = format === 'pdf' ? 'pdf' : 'csv';
  link.download = `${reportType}-${fundId}.${ext}`;
  document.body.appendChild(link);
  link.click();
  document.body.removeChild(link);
  window.URL.revokeObjectURL(downloadUrl);
}
