import { ChangeDetectionStrategy, Component, computed, inject } from '@angular/core';
import { DatePipe } from '@angular/common';
import { RouterLink } from '@angular/router';
import { rxResource } from '@angular/core/rxjs-interop';
import { map } from 'rxjs';

import { ButtonModule } from 'primeng/button';
import { TableModule } from 'primeng/table';
import { TagModule } from 'primeng/tag';
import { SkeletonModule } from 'primeng/skeleton';

import { CryptoManagerApi } from '../../../core/api/cryptomanager-api.service';
import { KeySummaryDto } from '../../../core/api/models';

type DashboardStats = {
  totalKeys: number;
  activeKeys: number;
  totalVersions: number;
  createdLast7Days: number;
};

@Component({
  imports: [
    DatePipe,
    RouterLink,
    ButtonModule,
    TableModule,
    TagModule,
    SkeletonModule
  ],
  templateUrl: './dashboard.component.html',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class DashboardComponent {
  private api = inject(CryptoManagerApi);

  keysResource = rxResource({
    stream: () => this.api.listKeys().pipe(map(res => res.keys ?? [])),
  });

  stats = computed<DashboardStats | undefined>(() => {
    const keys = this.keysResource.value();
    if (!keys) return undefined;
    return this.computeStats(keys);
  });

  recentKeys = computed<KeySummaryDto[]>(() => {
    const keys = this.keysResource.value();
    return (keys ?? []).slice(0, 5);
  });

  refresh() {
    this.keysResource.reload();
  }

  private computeStats(keys: KeySummaryDto[]): DashboardStats {
    const now = new Date();
    const weekAgo = new Date(now);
    weekAgo.setDate(now.getDate() - 7);

    const totalKeys = keys.length;

    const activeKeys = keys.filter(k =>
      (k.state ?? '').toLowerCase().includes('active')
    ).length;

    const totalVersions = keys.reduce((sum, k) => sum + (k.versionCount ?? 0), 0);

    const createdLast7Days = keys.filter(k => new Date(k.createdAt) >= weekAgo).length;

    return { totalKeys, activeKeys, totalVersions, createdLast7Days };
  }

  stateSeverity(state?: string | null) {
    const s = (state ?? '').toLowerCase();
    if (s.includes('active')) return 'success';
    if (s.includes('pending')) return 'info';
    if (s.includes('disabled')) return 'warn';
    if (s.includes('revoked') || s.includes('error')) return 'danger';
    return 'info';
  }
}
