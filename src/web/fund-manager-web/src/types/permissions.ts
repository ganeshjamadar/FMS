export type FundRole = 'Admin' | 'Editor' | 'Guest';

export interface Permissions {
  /** Current user's role in this fund */
  role: FundRole | null;
  isLoading: boolean;

  /** Can create/edit fund, manage members, approve loans, initiate dissolution */
  canManageFund: boolean;
  /** Can record payments, request loans, cast votes */
  canWrite: boolean;
  /** Can view data (all members) */
  canRead: boolean;
  /** Can export reports */
  canExport: boolean;
  /** Can view audit logs */
  canViewAudit: boolean;
}
