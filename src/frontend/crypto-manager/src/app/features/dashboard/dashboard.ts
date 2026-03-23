import { ChangeDetectionStrategy, Component, computed, inject } from '@angular/core';
import { DatePipe } from '@angular/common';
import { RouterLink } from '@angular/router';

import { ButtonModule } from 'primeng/button';
import { TableModule } from 'primeng/table';
import { TagModule } from 'primeng/tag';
import { SkeletonModule } from 'primeng/skeleton';

import { KeysService } from '../keys/keys.service';
import { KeySummaryDto } from '../../core/api/models';
import { Card } from '../../shared/components/card/card';
import { stateSeverity } from '../keys/keys.utils';

type DashboardStats = {
  totalKeys: number;
  activeKeys: number;
  totalVersions: number;
  createdLast7Days: number;
};

@Component({
  imports: [DatePipe, RouterLink, ButtonModule, TableModule, TagModule, SkeletonModule, Card],
  templateUrl: './dashboard.html',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class DashboardPage {
  protected keysService = inject(KeysService);
  protected readonly stateSeverity = stateSeverity;

  stats = computed<DashboardStats | undefined>(() => {
    const keys = this.keysService.keysResource.value();
    if (!keys) return undefined;
    return this.computeStats(keys);
  });

  recentKeys = computed<KeySummaryDto[]>(() =>
    (this.keysService.keysResource.value() ?? []).slice(0, 5)
  );

  private computeStats(keys: KeySummaryDto[]): DashboardStats {
    const now = new Date();
    const weekAgo = new Date(now);
    weekAgo.setDate(now.getDate() - 7);

    return {
      totalKeys: keys.length,
      activeKeys: keys.filter(k => (k.state ?? '').toLowerCase().includes('active')).length,
      totalVersions: keys.reduce((sum, k) => sum + (k.versionCount ?? 0), 0),
      createdLast7Days: keys.filter(k => new Date(k.createdAt) >= weekAgo).length,
    };
  }
}
