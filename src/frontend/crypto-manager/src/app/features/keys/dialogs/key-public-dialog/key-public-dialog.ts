import { ChangeDetectionStrategy, Component, effect, inject, input, output, signal } from '@angular/core';
import { FormBuilder, ReactiveFormsModule } from '@angular/forms';

import { ButtonModule } from 'primeng/button';
import { DialogModule } from 'primeng/dialog';
import { InputNumberModule } from 'primeng/inputnumber';
import { MessageModule } from 'primeng/message';
import { ProgressSpinner } from 'primeng/progressspinner';
import { TextareaModule } from 'primeng/textarea';

import { DownloadService } from '../../../../core/services/download.service';
import { KeySummaryDto } from '../../../../core/api/models';
import { KeysService } from '../../keys.service';

@Component({
  selector: 'app-key-public-dialog',
  imports: [
    ReactiveFormsModule,
    DialogModule,
    ButtonModule,
    InputNumberModule,
    TextareaModule,
    MessageModule,
    ProgressSpinner,
  ],
  templateUrl: './key-public-dialog.html',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class KeyPublicDialog {
  private keysService = inject(KeysService);
  private downloadService = inject(DownloadService);
  private fb = inject(FormBuilder);

  visible = input.required<boolean>();
  key = input<KeySummaryDto | null>(null);
  visibleChange = output<boolean>();

  loading = signal(false);
  error = signal<string | null>(null);
  publicPem = signal<string | null>(null);

  form = this.fb.group({
    version: this.fb.control<number | null>(null),
  });

  constructor() {
    effect(() => {
      if (this.visible()) {
        this.publicPem.set(null);
        this.error.set(null);
        this.loading.set(false);
        this.form.reset({ version: null });
      }
    });
  }

  fetch() {
    const keyId = this.key()?.keyId;
    if (!keyId) return;

    const { version } = this.form.getRawValue();
    this.publicPem.set(null);
    this.loading.set(true);

    this.keysService.getPublicKey(keyId, version).subscribe({
      next: (res) => {
        this.publicPem.set(res.publicKeyPem ?? '');
        this.error.set(null);
      },
      error: (err) => {
        this.error.set('Failed to fetch public key: ' + (err?.error?.Error || err.message || 'Unknown error'));
        this.loading.set(false);
      },
      complete: () => {
        this.loading.set(false);
      },
    });
  }

  copy(text: string | null | undefined) {
    if (text) navigator.clipboard.writeText(text);
  }

  download() {
    const pem = this.publicPem();
    if (pem) this.downloadService.downloadPem(pem, 'pubkey.pem');
  }
}