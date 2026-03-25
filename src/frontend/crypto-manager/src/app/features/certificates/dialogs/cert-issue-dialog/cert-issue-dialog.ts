import { ChangeDetectionStrategy, Component, inject, input, output, signal } from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';

import { ButtonModule } from 'primeng/button';
import { DialogModule } from 'primeng/dialog';
import { InputTextModule } from 'primeng/inputtext';
import { MessageModule } from 'primeng/message';
import { ProgressSpinner } from 'primeng/progressspinner';

import { EnrollCertificateResponseDto } from '../../../../core/api/models';
import { AppMessenger } from '../../../../core/services/app-messenger';
import { CertificatesService } from '../../certificates.service';

@Component({
  selector: 'app-cert-issue-dialog',
  imports: [
    ReactiveFormsModule,
    DialogModule,
    ButtonModule,
    InputTextModule,
    MessageModule,
    ProgressSpinner,
  ],
  templateUrl: './cert-issue-dialog.html',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class CertIssueDialog {
  private fb = inject(FormBuilder);
  private certificatesService = inject(CertificatesService);
  private appMessenger = inject(AppMessenger);

  visible = input.required<boolean>();
  keyId = input.required<string>();
  visibleChange = output<boolean>();
  enrolled = output<EnrollCertificateResponseDto>();

  loading = signal(false);
  error = signal<string | null>(null);

  form = this.fb.group({
    commonName: ['', [Validators.required, Validators.maxLength(64)]],
    organization: [''],
    organizationalUnit: [''],
    country: ['', [Validators.pattern(/^[A-Za-z]{2}$/)]],
  });

  close(): void {
    this.error.set(null);
    this.form.reset();
    this.visibleChange.emit(false);
  }

  submit(): void {
    if (this.form.invalid || !this.keyId()) return;

    const value = this.form.getRawValue();
    this.loading.set(true);

    this.certificatesService.enrollCertificate(this.keyId(), {
      commonName: value.commonName!,
      organization: value.organization || null,
      organizationalUnit: value.organizationalUnit || null,
      country: value.country || null,
    }).subscribe({
      next: result => {
        this.enrolled.emit(result);
        this.error.set(null);
        this.close();
        this.appMessenger.showMessage('success', 'Certificate enrollment started');
      },
      error: err => {
        this.error.set('Failed to start certificate enrollment: ' + (err?.error?.Error || err?.message || 'Unknown error'));
        this.loading.set(false);
      },
      complete: () => {
        this.loading.set(false);
      }
    });
  }
}
