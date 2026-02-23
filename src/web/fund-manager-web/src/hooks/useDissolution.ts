import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query';
import apiClient from '@/services/apiClient';
import type { DissolutionSettlement, SettlementDetail } from '@/types/dissolution';

export const dissolutionKeys = {
  all: ['dissolution'] as const,
  settlement: (fundId: string) => [...dissolutionKeys.all, fundId] as const,
};

export function useSettlement(fundId: string) {
  return useQuery({
    queryKey: dissolutionKeys.settlement(fundId),
    queryFn: async () => {
      const { data } = await apiClient.get<SettlementDetail>(
        `/funds/${fundId}/dissolution/settlement`
      );
      return data;
    },
  });
}

export function useInitiateDissolution(fundId: string) {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: async () => {
      const { data } = await apiClient.post<DissolutionSettlement>(
        `/funds/${fundId}/dissolution/initiate`
      );
      return data;
    },
    onSuccess: () => {
      qc.invalidateQueries({ queryKey: dissolutionKeys.settlement(fundId) });
    },
  });
}

export function useRecalculateSettlement(fundId: string) {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: async () => {
      const { data } = await apiClient.post<SettlementDetail>(
        `/funds/${fundId}/dissolution/settlement/recalculate`
      );
      return data;
    },
    onSuccess: () => {
      qc.invalidateQueries({ queryKey: dissolutionKeys.settlement(fundId) });
    },
  });
}

export function useConfirmDissolution(fundId: string) {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: async () => {
      const { data } = await apiClient.post<DissolutionSettlement>(
        `/funds/${fundId}/dissolution/confirm`
      );
      return data;
    },
    onSuccess: () => {
      qc.invalidateQueries({ queryKey: dissolutionKeys.settlement(fundId) });
    },
  });
}

export function useDownloadReport(fundId: string) {
  return useMutation({
    mutationFn: async (format: 'pdf' | 'csv') => {
      const { data } = await apiClient.get(
        `/funds/${fundId}/dissolution/report?format=${format}`,
        { responseType: 'blob' }
      );
      // Trigger download
      const blob = new Blob([data]);
      const url = URL.createObjectURL(blob);
      const a = document.createElement('a');
      a.href = url;
      a.download = `settlement-${fundId}.${format}`;
      a.click();
      URL.revokeObjectURL(url);
    },
  });
}
