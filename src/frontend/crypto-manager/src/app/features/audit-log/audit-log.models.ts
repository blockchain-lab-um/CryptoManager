export interface AuditLogEntry {
  id: string;
  timestamp: string; // ISO 8601
  actor: string;
  actorId?: string | null;
  action: string;
  keyId?: string | null;
  keyVersion?: number | null;
  mechanism?: string | null;
  success: boolean;
  error?: string | null;
}

export interface AuditLogFilter {
  from: Date | null;
  to: Date | null;
  action: string | null;
  keyId: string | null;
  /** Populated only by admins; always null for regular users. */
  actor: string | null;
}

export interface PagedAuditLogResult {
  items: AuditLogEntry[];
  page: number;
  pageSize: number;
  totalCount: number;
  totalPages: number;
}
