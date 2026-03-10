import { ChangeDetectionStrategy, Component, effect, inject, input, output, signal } from '@angular/core';

import { ButtonModule } from 'primeng/button';
import { DialogModule } from 'primeng/dialog';
import { MessageModule } from 'primeng/message';
import { ProgressSpinnerModule } from 'primeng/progressspinner';
import { TextareaModule } from 'primeng/textarea';

import { CryptoManagerApi } from '../../../../core/api/cryptomanager-api.service';
import { CryptoService } from '../../../../core/services/crypto.service';
import { KeySummaryDto, RotateKeyResponseDto } from '../../../../core/api/models';

@Component({
  selector: 'app-key-rotate-dialog',
  imports: [
    DialogModule,
    ButtonModule,
    TextareaModule,
    MessageModule,
    ProgressSpinnerModule,
  ],
  templateUrl: './key-rotate-dialog.component.html',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class KeyRotateDialogComponent {
  private api = inject(CryptoManagerApi);
  private cryptoService = inject(CryptoService);

  visible = input.required<boolean>();
  key = input<KeySummaryDto | null>(null);
  visibleChange = output<boolean>();
  rotated = output<void>();

  rotating = signal(false);
  rotateResult = signal<RotateKeyResponseDto | null>(null);
  error = signal<string | null>(null);

  constructor() {
    effect(() => {
      if (this.visible()) {
        this.rotateResult.set(null);
        this.rotating.set(false);
        this.error.set(null);
      }
    });
  }

  close() {
    this.visibleChange.emit(false);
  }

  rotate() {
    const keyId = this.key()?.keyId;
    if (!keyId) return;

    this.rotating.set(true);
    this.api.rotateKey(keyId).subscribe({
      next: (res) => {
        this.rotateResult.set(res);
        this.rotating.set(false);
        this.rotated.emit();
      },
      error: (e) => {
        this.error.set(e?.error?.message ?? 'Failed to rotate key');
        this.rotating.set(false);
      },
    });
  }

  copy(text: string | null | undefined) {
    if (text) navigator.clipboard.writeText(text);
  }

  downloadRotatedKey() {
    const pem = this.rotateResult()?.publicKeyPem;
    if (pem) this.cryptoService.downloadPem(pem, 'pubkey.pem');
  }
}
