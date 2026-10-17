import { Injectable, inject, signal } from '@angular/core';
import { rxResource } from '@angular/core/rxjs-interop';
import { CryptoManagerApi } from '../../core/api/cryptomanager-api.service';
import { AuditLogQueryParams } from '../../core/api/models';
import { AuditLogFilter } from './audit-log.models';

const EMPTY_FILTER: AuditLogFilter = {
  from: null,
  to: null,
  action: null,
  keyId: null,
  actor: null,
};

@Injectable({ providedIn: 'root' })
export class AuditLogService {
  private api = inject(CryptoManagerApi);

  // Committed filter state — what the server was last queried with.
  // Draft/pending state lives in the page component.
  readonly activeFilter = signal<AuditLogFilter>({ ...EMPTY_FILTER });
  readonly page = signal(1);
  readonly pageSize = signal(25);

  readonly auditResource = rxResource({
    stream: () => this.api.getAuditLog(this.toQueryParams({
      filter: this.activeFilter(),
      page: this.page(),
      pageSize: this.pageSize(),
    })),
  });

  /** Commits a new filter, resets to page 1, and re-fetches. */
  applyFilter(filter: AuditLogFilter): void {
    this.activeFilter.set(filter);
    this.page.set(1);
    this.auditResource.reload();
  }

  /** Clears all filters, resets to page 1, and re-fetches. */
  clearFilter(): void {
    this.applyFilter({ ...EMPTY_FILTER });
  }

  setPage(page: number): void {
    this.page.set(page);
    this.auditResource.reload();
  }

  reload(): void {
    this.auditResource.reload();
  }

  private toQueryParams(args: {
    filter: AuditLogFilter;
    page: number;
    pageSize: number;
  }): AuditLogQueryParams {
    const params: AuditLogQueryParams = {
      page: args.page,
      pageSize: args.pageSize,
    };
    if (args.filter.from) params.from = args.filter.from.toISOString();
    if (args.filter.to) params.to = args.filter.to.toISOString();
    if (args.filter.action) params.action = args.filter.action;
    if (args.filter.keyId) params.keyId = args.filter.keyId;
    if (args.filter.actor) params.actor = args.filter.actor;
    return params;
  }
}
