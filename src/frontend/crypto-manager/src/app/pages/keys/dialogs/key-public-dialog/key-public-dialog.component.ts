import { ChangeDetectionStrategy, Component, effect, inject, input, output, signal } from '@angular/core';
import { FormBuilder, ReactiveFormsModule } from '@angular/forms';

import { ButtonModule } from 'primeng/button';
import { DialogModule } from 'primeng/dialog';
import { InputNumberModule } from 'primeng/inputnumber';
import { MessageModule } from 'primeng/message';
import { TextareaModule } from 'primeng/textarea';

import { CryptoManagerApi } from '../../../../core/api/cryptomanager-api.service';
import { CryptoService } from '../../../../core/services/crypto.service';
import { KeySummaryDto } from '../../../../core/api/models';

@Component({
  selector: 'app-key-public-dialog',
  imports: [
    ReactiveFormsModule,
    DialogModule,
    ButtonModule,
    InputNumberModule,
    TextareaModule,
    MessageModule,
  ],
  templateUrl: './key-public-dialog.component.html',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class KeyPublicDialogComponent {
  private api = inject(CryptoManagerApi);
  private cryptoService = inject(CryptoService);
  private fb = inject(FormBuilder);

  visible = input.required<boolean>();
  key = input<KeySummaryDto | null>(null);
  visibleChange = output<boolean>();

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
        this.form.reset({ version: null });
      }
    });
  }

  fetch() {
    const keyId = this.key()?.keyId;
    if (!keyId) return;

    const { version } = this.form.getRawValue();
    this.publicPem.set(null);

    this.api.getPublicKey(keyId, version).subscribe({
      next: (res) => this.publicPem.set(res.publicKeyPem ?? ''),
      error: (e) => this.error.set(e?.error?.message ?? 'Failed to fetch public key'),
    });
  }

  copy(text: string | null | undefined) {
    if (text) navigator.clipboard.writeText(text);
  }

  download() {
    const pem = this.publicPem();
    if (pem) this.cryptoService.downloadPem(pem, 'pubkey.pem');
  }
}
