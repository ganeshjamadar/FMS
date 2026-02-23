import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query';
import apiClient from '@/services/apiClient';
import type {
  ContributionDue,
  TransactionEntry,
  ContributionSummary,
  RecordPaymentRequest,
  PaymentResult,
  PaginatedResponse,
} from '@/types/contribution';

export const contributionKeys = {
  all: ['contributions'] as const,
  dues: (fundId: string) => [...contributionKeys.all, 'dues', fundId] as const,
  duesList: (fundId: string, filters: Record<string, unknown>) =>
    [...contributionKeys.dues(fundId), filters] as const,
  due: (fundId: string, dueId: string) =>
    [...contributionKeys.dues(fundId), dueId] as const,
  summary: (fundId: string, monthYear: number) =>
    [...contributionKeys.all, 'summary', fundId, monthYear] as const,
  ledger: (fundId: string, filters: Record<string, unknown>) =>
    [...contributionKeys.all, 'ledger', fundId, filters] as const,
};

export function useDues(
  fundId: string,
  filters: { monthYear?: number; userId?: string; status?: string; page?: number; pageSize?: number } = {},
) {
  return useQuery({
    queryKey: contributionKeys.duesList(fundId, filters),
    queryFn: async () => {
      const params = new URLSearchParams();
      if (filters.monthYear) params.set('monthYear', String(filters.monthYear));
      if (filters.userId) params.set('userId', filters.userId);
      if (filters.status) params.set('status', filters.status);
      params.set('page', String(filters.page ?? 1));
      params.set('pageSize', String(filters.pageSize ?? 50));
      const { data } = await apiClient.get<PaginatedResponse<ContributionDue>>(
        `/funds/${fundId}/contributions/dues?${params}`,
      );
      return data;
    },
    enabled: !!fundId,
  });
}

export function useDue(fundId: string, dueId: string) {
  return useQuery({
    queryKey: contributionKeys.due(fundId, dueId),
    queryFn: async () => {
      const { data } = await apiClient.get<ContributionDue>(
        `/funds/${fundId}/contributions/dues/${dueId}`,
      );
      return data;
    },
    enabled: !!fundId && !!dueId,
  });
}

export function useContributionSummary(fundId: string, monthYear: number) {
  return useQuery({
    queryKey: contributionKeys.summary(fundId, monthYear),
    queryFn: async () => {
      const { data } = await apiClient.get<ContributionSummary>(
        `/funds/${fundId}/contributions/summary?monthYear=${monthYear}`,
      );
      return data;
    },
    enabled: !!fundId && !!monthYear,
  });
}

export function useLedger(
  fundId: string,
  filters: { type?: string; userId?: string; fromDate?: string; toDate?: string; page?: number; pageSize?: number } = {},
) {
  return useQuery({
    queryKey: contributionKeys.ledger(fundId, filters),
    queryFn: async () => {
      const params = new URLSearchParams();
      if (filters.type) params.set('type', filters.type);
      if (filters.userId) params.set('userId', filters.userId);
      if (filters.fromDate) params.set('fromDate', filters.fromDate);
      if (filters.toDate) params.set('toDate', filters.toDate);
      params.set('page', String(filters.page ?? 1));
      params.set('pageSize', String(filters.pageSize ?? 50));
      const { data } = await apiClient.get<PaginatedResponse<TransactionEntry>>(
        `/funds/${fundId}/contributions/ledger?${params}`,
      );
      return data;
    },
    enabled: !!fundId,
  });
}

export function useGenerateDues(fundId: string) {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: async (monthYear: number) => {
      const { data } = await apiClient.post(
        `/funds/${fundId}/contributions/dues/generate`,
        { monthYear },
      );
      return data as { generated: number; skipped: number };
    },
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: contributionKeys.dues(fundId) });
    },
  });
}

export function useRecordPayment(fundId: string) {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: async ({
      request,
      idempotencyKey,
      version,
    }: {
      request: RecordPaymentRequest;
      idempotencyKey: string;
      version: string;
    }) => {
      const { data } = await apiClient.post<PaymentResult>(
        `/funds/${fundId}/contributions/payments`,
        request,
        {
          headers: {
            'Idempotency-Key': idempotencyKey,
            'If-Match': version,
          },
        },
      );
      return data;
    },
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: contributionKeys.dues(fundId) });
      queryClient.invalidateQueries({ queryKey: contributionKeys.ledger(fundId, {}) });
    },
  });
}
