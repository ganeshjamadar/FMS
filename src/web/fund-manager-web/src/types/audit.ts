export interface AuditLogSummary {
  id: string;
  actorId: string;
  actorName?: string;
  timestamp: string;
  actionType: string;
  entityType: string;
  entityId: string;
  serviceName: string;
}

export interface AuditLogDetail extends AuditLogSummary {
  beforeState: Record<string, unknown> | null;
  afterState: Record<string, unknown> | null;
  ipAddress: string | null;
  userAgent: string | null;
  correlationId: string | null;
}

export interface PaginatedAuditLogs {
  items: AuditLogSummary[];
  totalCount: number;
  page: number;
  pageSize: number;
}
