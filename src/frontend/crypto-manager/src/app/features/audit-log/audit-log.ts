import { ChangeDetectionStrategy, Component, computed, inject, signal } from '@angular/core';
import { DatePipe } from '@angular/common';
import { FormsModule } from '@angular/forms';

import { ButtonModule } from 'primeng/button';
import { DatePickerModule } from 'primeng/datepicker';
import { InputTextModule } from 'primeng/inputtext';
import { SelectModule } from 'primeng/select';
import { TableLazyLoadEvent, TableModule } from 'primeng/table';
import { TagModule } from 'primeng/tag';

import { AuthService } from '../../auth/auth.service';
import { Card } from '../../shared/components/card/card';
import { KeysService } from '../keys/keys.service';
import { toKeySelectOptions } from '../keys/keys.utils';
import { AuditLogFilter } from './audit-log.models';
import { AuditLogService } from './audit-log.service';
import { actionBadgeClass, actionLabel, AUDIT_ACTION_OPTIONS, outcomeSeverity } from './audit-log.utils';

const EMPTY_FILTER: AuditLogFilter = {
  from: null, to: null, action: null, keyId: null, actor: null,
};

@Component({
  imports: [
    DatePipe,
    FormsModule,
    ButtonModule,
    DatePickerModule,
    InputTextModule,
    SelectModule,
    TableModule,
    TagModule,
    Card,
  ],
  templateUrl: './audit-log.html',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class AuditLogPage {
  protected readonly auditLogService = inject(AuditLogService);
  protected readonly authService = inject(AuthService);
  protected readonly keysService = inject(KeysService);
  protected readonly outcomeSeverity = outcomeSeverity;
  protected readonly actionLabel = actionLabel;
  protected readonly actionBadgeClass = actionBadgeClass;
  protected readonly actionOptions = AUDIT_ACTION_OPTIONS;
  protected readonly keyOptions = computed(() =>
    toKeySelectOptions(this.keysService.keysResource.value() ?? []).map(option => ({
      ...option,
      name: option.label,
      label: option.searchText || option.label,
    })));

  readonly isAdmin = this.authService.isAdmin;

  // Draft filter state — not committed until the user clicks Apply.
  pendingFilter = signal<AuditLogFilter>({ ...EMPTY_FILTER });

  onFromChange(date: Date | null)    { this.pendingFilter.update(f => ({ ...f, from: date })); }
  onToChange(date: Date | null)      { this.pendingFilter.update(f => ({ ...f, to: date })); }
  onActionChange(value: string|null) { this.pendingFilter.update(f => ({ ...f, action: value })); }
  onKeyIdChange(value: string | null) { this.pendingFilter.update(f => ({ ...f, keyId: value || null })); }
  onActorChange(value: string)       { this.pendingFilter.update(f => ({ ...f, actor: value || null })); }

  applyFilter() {
    this.auditLogService.applyFilter(this.pendingFilter());
  }

  clearFilter() {
    this.pendingFilter.set({ ...EMPTY_FILTER });
    this.auditLogService.clearFilter();
  }

  onLazyLoad(event: TableLazyLoadEvent) {
    const newPage = Math.floor((event.first ?? 0) / (event.rows ?? 25)) + 1;
    this.auditLogService.setPage(newPage);
  }
}
