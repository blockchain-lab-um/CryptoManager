import { Injectable, inject, signal } from '@angular/core';
import { rxResource } from '@angular/core/rxjs-interop';
import { EMPTY, Subscription, catchError, interval, of, switchMap, tap, throwError } from 'rxjs';

import { CryptoManagerApi } from '../../core/api/cryptomanager-api.service';
import {
  ActiveCertificateDto,
  EnrollCertificateRequestDto,
  ImportCertificateRequestDto,
  RevokeCertificateRequestDto,
} from '../../core/api/models';

@Injectable({ providedIn: 'root' })
export class CertificatesService {
  private api = inject(CryptoManagerApi);
  private pendingRefreshSub: Subscription | null = null;

  readonly keyId = signal<string | null>(null);
  readonly currentEnrollmentId = signal<string | null>(null);

  readonly activeCertificateResource = rxResource({
    stream: () => {
      const keyId = this.keyId();
      if (!keyId) return of(null as ActiveCertificateDto | null);
      return this.api.getActiveCertificate(keyId).pipe(
        catchError(err => err.status === 404 ? of(null) : throwError(() => err))
      );
    },
  });

  setKeyId(keyId: string | null): void {
    if (this.keyId() === keyId) return;
    this.stopPendingRefresh();
    this.currentEnrollmentId.set(null);
    this.keyId.set(keyId);
    this.activeCertificateResource.reload();
  }

  reload(): void {
    this.activeCertificateResource.reload();
  }

  startPendingRefresh(): void {
    if (this.pendingRefreshSub) return;
    if (!this.keyId() || !this.currentEnrollmentId()) return;

    this.pendingRefreshSub = interval(10_000).pipe(
      switchMap(() => {
        const keyId = this.keyId();
        const enrollmentId = this.currentEnrollmentId();
        if (!keyId || !enrollmentId) return EMPTY;
        return this.api.completeEnrollment(keyId, enrollmentId);
      })
    ).subscribe({
      next: result => {
        if (!result.isComplete) return;
        this.currentEnrollmentId.set(null);
        this.stopPendingRefresh();
        this.reload();
      },
      error: () => {
        this.stopPendingRefresh();
      },
    });
  }

  stopPendingRefresh(): void {
    this.pendingRefreshSub?.unsubscribe();
    this.pendingRefreshSub = null;
  }

  enrollCertificate(keyId: string, body: EnrollCertificateRequestDto) {
    return this.api.enrollCertificate(keyId, body).pipe(
      tap(result => {
        this.currentEnrollmentId.set(result.enrollmentId);
        this.startPendingRefresh();
      })
    );
  }

  completeEnrollment(keyId: string, enrollmentId: string) {
    return this.api.completeEnrollment(keyId, enrollmentId).pipe(
      tap(result => {
        if (!result.isComplete) return;
        this.currentEnrollmentId.set(null);
        this.stopPendingRefresh();
        this.reload();
      })
    );
  }

  importCertificate(keyId: string, body: ImportCertificateRequestDto) {
    return this.api.importCertificate(keyId, body).pipe(
      tap(() => {
        this.currentEnrollmentId.set(null);
        this.stopPendingRefresh();
        this.reload();
      })
    );
  }

  revokeCertificate(keyId: string, certificateId: string, body: RevokeCertificateRequestDto) {
    return this.api.revokeCertificate(keyId, certificateId, body).pipe(
      tap(() => {
        this.currentEnrollmentId.set(null);
        this.stopPendingRefresh();
        this.reload();
      })
    );
  }
}
