import { ChangeDetectionStrategy, Component, inject, input, output, signal } from '@angular/core';
import { FormBuilder, ReactiveFormsModule } from '@angular/forms';

import { ButtonModule } from 'primeng/button';
import { DialogModule } from 'primeng/dialog';
import { MessageModule } from 'primeng/message';
import { ProgressSpinner } from 'primeng/progressspinner';
import { SelectModule } from 'primeng/select';

import { ActiveCertificateDto } from '../../../../core/api/models';
import { AppMessenger } from '../../../../core/services/app-messenger';
import { CertificatesService } from '../../certificates.service';

const REVOCATION_REASON_OPTIONS = [
  { label: 'Unspecified', value: 'Unspecified' },
  { label: 'Key Compromise', value: 'KeyCompromise' },
  { label: 'CA Compromise', value: 'CACompromise' },
  { label: 'Affiliation Changed', value: 'AffiliationChanged' },
  { label: 'Superseded', value: 'Superseded' },
  { label: 'Cessation Of Operation', value: 'CessationOfOperation' },
];

@Component({
  selector: 'app-cert-revoke-dialog',
  imports: [
    ReactiveFormsModule,
    DialogModule,
    ButtonModule,
    MessageModule,
    ProgressSpinner,
    SelectModule,
  ],
  templateUrl: './cert-revoke-dialog.html',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class CertRevokeDialog {
  private fb = inject(FormBuilder);
  private certificatesService = inject(CertificatesService);
  private appMessenger = inject(AppMessenger);

  visible = input.required<boolean>();
  keyId = input.required<string>();
  cert = input<ActiveCertificateDto | null>(null);
  visibleChange = output<boolean>();
  revoked = output<void>();

  readonly reasonOptions = REVOCATION_REASON_OPTIONS;

  loading = signal(false);
  error = signal<string | null>(null);

  form = this.fb.group({
    reason: this.fb.control<string>('Unspecified', { nonNullable: true }),
  });

  close(): void {
    this.error.set(null);
    this.form.reset({ reason: 'Unspecified' });
    this.visibleChange.emit(false);
  }

  submit(): void {
    const cert = this.cert();
    const keyId = this.keyId();
    if (!cert?.id || !keyId) return;

    this.loading.set(true);

    this.certificatesService.revokeCertificate(keyId, cert.id, {
      reason: this.form.getRawValue().reason,
    }).subscribe({
      next: () => {
        this.revoked.emit();
        this.close();
        this.appMessenger.showMessage('warn', 'Certificate revoked');
      },
      error: err => {
        this.error.set('Failed to revoke certificate: ' + (err?.error?.Error || err?.message || 'Unknown error'));
        this.loading.set(false);
      },
      complete: () => {
        this.loading.set(false);
      },
    });
  }
}
