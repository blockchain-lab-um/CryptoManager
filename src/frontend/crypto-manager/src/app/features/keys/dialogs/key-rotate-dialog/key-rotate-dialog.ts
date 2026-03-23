import { ChangeDetectionStrategy, Component, effect, inject, input, output, signal } from '@angular/core';

import { ButtonModule } from 'primeng/button';
import { DialogModule } from 'primeng/dialog';
import { MessageModule } from 'primeng/message';
import { ProgressSpinnerModule } from 'primeng/progressspinner';
import { TextareaModule } from 'primeng/textarea';

import { AppMessenger } from '../../../../core/services/app-messenger';
import { DownloadService } from '../../../../core/services/download.service';
import { KeySummaryDto, RotateKeyResponseDto } from '../../../../core/api/models';
import { KeysService } from '../../keys.service';

@Component({
  selector: 'app-key-rotate-dialog',
  imports: [
    DialogModule,
    ButtonModule,
    TextareaModule,
    MessageModule,
    ProgressSpinnerModule,
  ],
  templateUrl: './key-rotate-dialog.html',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class KeyRotateDialog {
  private keysService = inject(KeysService);
  private appMessenger = inject(AppMessenger);
  private downloadService = inject(DownloadService);

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
    this.keysService.rotateKey(keyId).subscribe({
      next: (res) => {
        this.rotateResult.set(res);
        this.error.set(null);
        this.rotated.emit();
        this.appMessenger.showMessage('success', 'Key rotated', `Key ${keyId} has been rotated to version ${res.newPrimaryVersion}.`);
      },
      error: (err) => {
        this.error.set('Failed to rotate key: ' + (err?.error?.Error || err.message || 'Unknown error'));
        this.rotating.set(false);
      },
      complete: () => {
        this.rotating.set(false);
      },
    });
  }

  copy(text: string | null | undefined) {
    if (text) navigator.clipboard.writeText(text);
  }

  downloadRotatedKey() {
    const pem = this.rotateResult()?.publicKeyPem;
    if (pem) this.downloadService.downloadPem(pem, 'pubkey.pem');
  }
}
