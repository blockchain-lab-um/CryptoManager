import { ChangeDetectionStrategy, Component, computed, inject } from '@angular/core';
import { DatePipe } from '@angular/common';
import { RouterLink } from '@angular/router';
import { firstValueFrom } from 'rxjs';

import { ButtonModule } from 'primeng/button';
import { TableModule } from 'primeng/table';
import { TagModule } from 'primeng/tag';
import { SkeletonModule } from 'primeng/skeleton';
import { MessageService } from 'primeng/api';

import { CryptoManagerApi } from '../../core/api/cryptomanager-api.service';
import { KeysService } from '../keys/keys.service';
import { KeySummaryDto } from '../../core/api/models';
import { DownloadService } from '../../core/services/download.service';
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
  private api = inject(CryptoManagerApi);
  private downloadService = inject(DownloadService);
  private messageService = inject(MessageService);
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

  protected async downloadSystemCaCertificate() {
    try {
      const response = await firstValueFrom(this.api.downloadSystemCaCertificate());
      const blob = response.body;
      if (!blob) {
        this.messageService.add({
          severity: 'error',
          summary: 'Download failed',
          detail: 'The system CA certificate could not be downloaded.'
        });
        return;
      }

      const filename =
        this.getFileNameFromContentDisposition(response.headers.get('content-disposition')) ??
        'cryptomanager-softca.cer';

      this.downloadService.downloadBuffer(await blob.arrayBuffer(), filename);
    } catch {
      this.messageService.add({
        severity: 'error',
        summary: 'Download failed',
        detail: 'The system CA certificate could not be downloaded.'
      });
    }
  }

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

  private getFileNameFromContentDisposition(header: string | null): string | null {
    if (!header) return null;
    const match = /filename\*?=(?:UTF-8''|\"?)([^\";]+)/i.exec(header);
    return match ? decodeURIComponent(match[1].replace(/\"/g, '')) : null;
  }
}
