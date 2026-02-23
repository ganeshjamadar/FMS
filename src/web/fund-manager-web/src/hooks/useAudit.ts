import { useQuery } from '@tanstack/react-query';
import { api } from '@/services/apiClient';
import type { PaginatedAuditLogs, AuditLogDetail } from '@/types/audit';

export const auditKeys = {
  all: (fundId: string) => ['audit', fundId] as const,
  logs: (fundId: string, params: Record<string, unknown>) =>
    ['audit', fundId, 'logs', params] as const,
  log: (fundId: string, logId: string) =>
    ['audit', fundId, 'logs', logId] as const,
  entityHistory: (fundId: string, entityType: string, entityId: string) =>
    ['audit', fundId, 'entity-history', entityType, entityId] as const,
};

export function useAuditLogs(
  fundId: string,
  fromDate: string,
  toDate: string,
  page = 1,
  filters?: {
    actorId?: string;
    actionType?: string;
    entityType?: string;
    entityId?: string;
  }
) {
  return useQuery({
    queryKey: auditKeys.logs(fundId, { fromDate, toDate, page, ...filters }),
    queryFn: () =>
      api.get<PaginatedAuditLogs>(`/funds/${fundId}/audit/logs`, {
        fromDate,
        toDate,
        page,
        pageSize: 50,
        ...filters,
      }),
    enabled: !!fundId && !!fromDate && !!toDate,
  });
}

export function useAuditLogDetail(fundId: string, logId: string) {
  return useQuery({
    queryKey: auditKeys.log(fundId, logId),
    queryFn: () =>
      api.get<AuditLogDetail>(`/funds/${fundId}/audit/logs/${logId}`),
    enabled: !!fundId && !!logId,
  });
}

export function useEntityHistory(
  fundId: string,
  entityType: string,
  entityId: string,
  fromDate: string,
  toDate: string
) {
  return useQuery({
    queryKey: auditKeys.entityHistory(fundId, entityType, entityId),
    queryFn: () =>
      api.get<AuditLogDetail[]>(`/funds/${fundId}/audit/entity-history`, {
        entityType,
        entityId,
        fromDate,
        toDate,
      }),
    enabled: !!fundId && !!entityType && !!entityId && !!fromDate && !!toDate,
  });
}
