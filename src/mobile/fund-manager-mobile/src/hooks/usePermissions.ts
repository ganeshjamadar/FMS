import { useQuery } from '@tanstack/react-query';
import apiClient from '../../services/apiClient';

export type FundRole = 'Admin' | 'Editor' | 'Guest';

export interface Permissions {
  role: FundRole | null;
  isLoading: boolean;
  canManageFund: boolean;
  canWrite: boolean;
  canRead: boolean;
  canExport: boolean;
  canViewAudit: boolean;
}

interface RoleResponse {
  role: FundRole;
}

export function usePermissions(fundId: string | undefined): Permissions {
  const { data, isLoading } = useQuery({
    queryKey: ['fund-role', fundId],
    queryFn: async () => {
      const response = await apiClient.get<RoleResponse>(
        `/api/funds/${fundId}/members/me/role`
      );
      return response.data;
    },
    enabled: !!fundId,
    staleTime: 5 * 60 * 1000,
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
