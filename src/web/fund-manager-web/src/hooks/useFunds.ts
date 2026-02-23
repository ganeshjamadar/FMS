import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query';
import { api } from '@/services/apiClient';
import type {
  Fund,
  FundDashboard,
  CreateFundRequest,
  UpdateFundRequest,
  MemberSummary,
  PaginatedResponse,
  FundState,
} from '@/types/fund';

// ── Query Keys ──

export const fundKeys = {
  all: ['funds'] as const,
  lists: () => [...fundKeys.all, 'list'] as const,
  list: (params: { state?: FundState; page?: number; pageSize?: number }) =>
    [...fundKeys.lists(), params] as const,
  details: () => [...fundKeys.all, 'detail'] as const,
  detail: (id: string) => [...fundKeys.details(), id] as const,
  dashboard: (id: string) => [...fundKeys.all, 'dashboard', id] as const,
  members: (fundId: string) => [...fundKeys.all, 'members', fundId] as const,
};

// ── Queries ──

export function useFunds(params: {
  state?: FundState;
  page?: number;
  pageSize?: number;
} = {}) {
  return useQuery({
    queryKey: fundKeys.list(params),
    queryFn: () =>
      api.get<PaginatedResponse<Fund>>('/funds', {
        state: params.state,
        page: params.page ?? 1,
        pageSize: params.pageSize ?? 20,
      }),
  });
}

export function useFund(fundId: string) {
  return useQuery({
    queryKey: fundKeys.detail(fundId),
    queryFn: () => api.get<Fund>(`/funds/${fundId}`),
    enabled: !!fundId,
  });
}

export function useFundDashboard(fundId: string) {
  return useQuery({
    queryKey: fundKeys.dashboard(fundId),
    queryFn: () => api.get<FundDashboard>(`/funds/${fundId}/dashboard`),
    enabled: !!fundId,
  });
}

export function useFundMembers(fundId: string, page = 1, pageSize = 20) {
  return useQuery({
    queryKey: [...fundKeys.members(fundId), { page, pageSize }],
    queryFn: () =>
      api.get<PaginatedResponse<MemberSummary>>(
        `/funds/${fundId}/members`,
        { page, pageSize },
      ),
    enabled: !!fundId,
  });
}

// ── Mutations ──

export function useCreateFund() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (data: CreateFundRequest) =>
      api.post<Fund>('/funds', data),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: fundKeys.lists() });
    },
  });
}

export function useUpdateFund(fundId: string) {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (data: UpdateFundRequest) =>
      api.patch<Fund>(`/funds/${fundId}`, data),
    onSuccess: (updatedFund) => {
      queryClient.setQueryData(fundKeys.detail(fundId), updatedFund);
      queryClient.invalidateQueries({ queryKey: fundKeys.lists() });
    },
  });
}

export function useActivateFund(fundId: string) {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: () => api.post<Fund>(`/funds/${fundId}/activate`),
    onSuccess: (updatedFund) => {
      queryClient.setQueryData(fundKeys.detail(fundId), updatedFund);
      queryClient.invalidateQueries({ queryKey: fundKeys.lists() });
      queryClient.invalidateQueries({ queryKey: fundKeys.dashboard(fundId) });
    },
  });
}
