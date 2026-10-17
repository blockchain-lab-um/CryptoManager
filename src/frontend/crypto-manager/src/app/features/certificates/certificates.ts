import { ChangeDetectionStrategy, Component, computed, inject, OnDestroy, OnInit, signal } from '@angular/core';
import { DatePipe } from '@angular/common';
import { ActivatedRoute, RouterLink } from '@angular/router';

import { ButtonModule } from 'primeng/button';
import { MessageModule } from 'primeng/message';
import { TagModule } from 'primeng/tag';

import { ActiveCertificateDto } from '../../core/api/models';
import { KeysService } from '../keys/keys.service';
import { CertificatesService } from './certificates.service';
import { Card } from '../../shared/components/card/card';
import { certStatusBadgeClass, isExpired, isExpiringSoon, sourceLabel, statusSeverity } from './certificates.utils';
import { CertIssueDialog } from './dialogs/cert-issue-dialog/cert-issue-dialog';
import { CertImportDialog } from './dialogs/cert-import-dialog/cert-import-dialog';
import { CertRevokeDialog } from './dialogs/cert-revoke-dialog/cert-revoke-dialog';
import { CertDetailDialog } from './dialogs/cert-detail-dialog/cert-detail-dialog';

@Component({
  imports: [
    DatePipe,
    RouterLink,
    ButtonModule,
    MessageModule,
    TagModule,
    Card,
    CertIssueDialog,
    CertImportDialog,
    CertRevokeDialog,
    CertDetailDialog,
  ],
  templateUrl: './certificates.html',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class CertificatesPage implements OnInit, OnDestroy {
  private route = inject(ActivatedRoute);

  protected readonly certificatesService = inject(CertificatesService);
  protected readonly keysService = inject(KeysService);
  protected readonly statusSeverity = statusSeverity;
  protected readonly sourceLabel = sourceLabel;
  protected readonly certStatusBadgeClass = certStatusBadgeClass;

  readonly keyId = signal<string | null>(null);
  readonly showIssue = signal(false);
  readonly showImport = signal(false);
  readonly showRevoke = signal(false);
  readonly showDetail = signal(false);
  readonly selectedCert = signal<ActiveCertificateDto | null>(null);

  readonly keyName = computed(() => {
    const keyId = this.keyId();
    if (!keyId) return null;
    const match = (this.keysService.keysResource.value() ?? []).find(k => k.keyId === keyId);
    return match?.name ?? keyId;
  });

  readonly activeCert = computed(() => this.certificatesService.activeCertificateResource.value());
  readonly hasActiveCert = computed(() => this.activeCert() !== null);
  readonly hasPendingEnrollment = computed(() => this.certificatesService.currentEnrollmentId() !== null);
  readonly certExpiringSoon = computed(() => {
    const cert = this.activeCert();
    if (!cert?.notAfter) return false;
    const now = new Date();
    return !isExpired(cert.notAfter, now) && isExpiringSoon(cert.notAfter, now);
  });

  ngOnInit(): void {
    const keyId = this.route.snapshot.paramMap.get('keyId');
    this.keyId.set(keyId);
    this.certificatesService.setKeyId(keyId);
  }

  ngOnDestroy(): void {
    this.certificatesService.stopPendingRefresh();
    this.certificatesService.setKeyId(null);
  }

  refresh(): void {
    const keyId = this.keyId();
    const enrollmentId = this.certificatesService.currentEnrollmentId();

    if (!keyId || !enrollmentId) {
      this.certificatesService.reload();
      return;
    }

    this.certificatesService.completeEnrollment(keyId, enrollmentId).subscribe({
      error: () => {
        // Let the resource/error UI remain the source of truth.
      },
    });
  }

  openIssue(): void {
    this.showIssue.set(true);
  }

  openImport(): void {
    this.showImport.set(true);
  }

  openDetail(): void {
    this.selectedCert.set(this.activeCert() ?? null);
    this.showDetail.set(true);
  }

  openRevoke(): void {
    this.selectedCert.set(this.activeCert() ?? null);
    this.showRevoke.set(true);
  }
}
