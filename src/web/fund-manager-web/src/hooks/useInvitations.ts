import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query';
import { api } from '@/services/apiClient';
import type {
  Invitation,
  InviteMemberRequest,
  AcceptInvitationRequest,
} from '@/types/invitation';
import type { PaginatedResponse } from '@/types/fund';
import { fundKeys } from './useFunds';

export const invitationKeys = {
  all: ['invitations'] as const,
  byFund: (fundId: string) => [...invitationKeys.all, fundId] as const,
};

export function useInvitations(fundId: string, page = 1, pageSize = 20) {
  return useQuery({
    queryKey: [...invitationKeys.byFund(fundId), { page, pageSize }],
    queryFn: () =>
      api.get<PaginatedResponse<Invitation>>(
        `/funds/${fundId}/invitations`,
        { page, pageSize },
      ),
    enabled: !!fundId,
  });
}

export function useInviteMember(fundId: string) {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (data: InviteMemberRequest) =>
      api.post<Invitation>(`/funds/${fundId}/invitations`, data),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: invitationKeys.byFund(fundId) });
    },
  });
}

export function useAcceptInvitation() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: ({
      invitationId,
      data,
    }: {
      invitationId: string;
      data: AcceptInvitationRequest;
    }) => api.post(`/invitations/${invitationId}/accept`, data),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: invitationKeys.all });
      queryClient.invalidateQueries({ queryKey: fundKeys.all });
    },
  });
}

export function useDeclineInvitation() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (invitationId: string) =>
      api.post(`/invitations/${invitationId}/decline`),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: invitationKeys.all });
    },
  });
}
