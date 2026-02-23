import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query';
import apiClient from '@/services/apiClient';
import type {
  Loan,
  LoanRequest,
  ApproveLoanRequest,
  RejectLoanRequest,
  PaginatedLoanList,
  RepaymentEntry,
  RecordRepaymentRequest,
  RepaymentResult,
  VotingSessionDetail,
  StartVotingRequest,
  CastVoteRequest,
  FinaliseVotingRequest,
  VotingSession,
} from '@/types/loan';

export const loanKeys = {
  all: ['loans'] as const,
  lists: () => [...loanKeys.all, 'list'] as const,
  list: (fundId: string, filters?: Record<string, unknown>) =>
    [...loanKeys.lists(), fundId, filters] as const,
  details: () => [...loanKeys.all, 'detail'] as const,
  detail: (fundId: string, loanId: string) =>
    [...loanKeys.details(), fundId, loanId] as const,
  repayments: (fundId: string, loanId: string) =>
    [...loanKeys.all, 'repayments', fundId, loanId] as const,
  voting: (loanId: string, sessionId: string) =>
    [...loanKeys.all, 'voting', loanId, sessionId] as const,
};

export function useLoans(
  fundId: string,
  filters?: { status?: string; borrowerId?: string; page?: number; pageSize?: number }
) {
  return useQuery({
    queryKey: loanKeys.list(fundId, filters),
    queryFn: async () => {
      const params = new URLSearchParams();
      if (filters?.status) params.set('status', filters.status);
      if (filters?.borrowerId) params.set('borrowerId', filters.borrowerId);
      if (filters?.page) params.set('page', String(filters.page));
      if (filters?.pageSize) params.set('pageSize', String(filters.pageSize));
      const { data } = await apiClient.get<PaginatedLoanList>(
        `/funds/${fundId}/loans?${params}`
      );
      return data;
    },
    enabled: !!fundId,
  });
}

export function useLoan(fundId: string, loanId: string) {
  return useQuery({
    queryKey: loanKeys.detail(fundId, loanId),
    queryFn: async () => {
      const { data } = await apiClient.get<Loan>(
        `/funds/${fundId}/loans/${loanId}`
      );
      return data;
    },
    enabled: !!fundId && !!loanId,
  });
}

export function useRepayments(fundId: string, loanId: string) {
  return useQuery({
    queryKey: loanKeys.repayments(fundId, loanId),
    queryFn: async () => {
      const { data } = await apiClient.get<RepaymentEntry[]>(
        `/funds/${fundId}/loans/${loanId}/repayments`
      );
      return data;
    },
    enabled: !!fundId && !!loanId,
  });
}

export function useRequestLoan(fundId: string) {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: async (req: LoanRequest) => {
      const { data } = await apiClient.post<Loan>(
        `/funds/${fundId}/loans`,
        req
      );
      return data;
    },
    onSuccess: () => {
      qc.invalidateQueries({ queryKey: loanKeys.lists() });
    },
  });
}

export function useApproveLoan(fundId: string) {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: async ({ loanId, req }: { loanId: string; req: ApproveLoanRequest }) => {
      const { data } = await apiClient.post<Loan>(
        `/funds/${fundId}/loans/${loanId}/approve`,
        req
      );
      return data;
    },
    onSuccess: () => {
      qc.invalidateQueries({ queryKey: loanKeys.all });
    },
  });
}

export function useRejectLoan(fundId: string) {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: async ({ loanId, req }: { loanId: string; req: RejectLoanRequest }) => {
      const { data } = await apiClient.post<Loan>(
        `/funds/${fundId}/loans/${loanId}/reject`,
        req
      );
      return data;
    },
    onSuccess: () => {
      qc.invalidateQueries({ queryKey: loanKeys.all });
    },
  });
}

export function useGenerateRepayment(fundId: string, loanId: string) {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: async (monthYear: number) => {
      const { data } = await apiClient.post<RepaymentEntry>(
        `/funds/${fundId}/loans/${loanId}/repayments/generate`,
        { monthYear }
      );
      return data;
    },
    onSuccess: () => {
      qc.invalidateQueries({ queryKey: loanKeys.repayments(fundId, loanId) });
    },
  });
}

export function useRecordRepayment(fundId: string, loanId: string) {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: async ({
      repaymentId,
      req,
      idempotencyKey,
      version,
    }: {
      repaymentId: string;
      req: RecordRepaymentRequest;
      idempotencyKey: string;
      version: string;
    }) => {
      const { data } = await apiClient.post<RepaymentResult>(
        `/funds/${fundId}/loans/${loanId}/repayments/${repaymentId}/pay`,
        req,
        {
          headers: {
            'Idempotency-Key': idempotencyKey,
            'If-Match': version,
          },
        }
      );
      return data;
    },
    onSuccess: () => {
      qc.invalidateQueries({ queryKey: loanKeys.all });
    },
  });
}

// --- Voting Hooks ---

export function useVotingSession(fundId: string, loanId: string, sessionId: string) {
  return useQuery({
    queryKey: loanKeys.voting(loanId, sessionId),
    queryFn: async () => {
      const { data } = await apiClient.get<VotingSessionDetail>(
        `/funds/${fundId}/loans/${loanId}/voting/${sessionId}`
      );
      return data;
    },
    enabled: !!sessionId,
  });
}

export function useStartVoting(fundId: string) {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: async ({ loanId, req }: { loanId: string; req: StartVotingRequest }) => {
      const { data } = await apiClient.post<VotingSession>(
        `/funds/${fundId}/loans/${loanId}/voting/start`,
        req
      );
      return data;
    },
    onSuccess: () => {
      qc.invalidateQueries({ queryKey: loanKeys.all });
    },
  });
}

export function useCastVote(fundId: string, loanId: string, sessionId: string) {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: async (req: CastVoteRequest) => {
      const { data } = await apiClient.post(
        `/funds/${fundId}/loans/${loanId}/voting/${sessionId}/vote`,
        req
      );
      return data;
    },
    onSuccess: () => {
      qc.invalidateQueries({ queryKey: loanKeys.voting(loanId, sessionId) });
    },
  });
}

export function useFinaliseVoting(fundId: string, loanId: string, sessionId: string) {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: async (req: FinaliseVotingRequest) => {
      const { data } = await apiClient.post(
        `/funds/${fundId}/loans/${loanId}/voting/${sessionId}/finalise`,
        req
      );
      return data;
    },
    onSuccess: () => {
      qc.invalidateQueries({ queryKey: loanKeys.all });
    },
  });
}
