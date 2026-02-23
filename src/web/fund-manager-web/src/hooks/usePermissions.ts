import { useQuery } from '@tanstack/react-query';
import { api } from '@/services/apiClient';
import type { FundRole, Permissions } from '@/types/permissions';

interface RoleResponse {
  role: FundRole;
}

export function usePermissions(fundId: string | undefined): Permissions {
  const { data, isLoading } = useQuery({
    queryKey: ['fund-role', fundId],
    queryFn: () => api.get<RoleResponse>(`/funds/${fundId}/members/me/role`),
    enabled: !!fundId,
    staleTime: 5 * 60 * 1000, // cache role for 5 minutes
    retry: false,
  });

  const role = data?.role ?? null;

  return {
    role,
    isLoading,
    canManageFund: role === 'Admin',
    canWrite: role === 'Admin' || role === 'Editor',
    canRead: role !== null,
    canExport: role === 'Admin' || role === 'Editor',
    canViewAudit: role === 'Admin',
  };
}
