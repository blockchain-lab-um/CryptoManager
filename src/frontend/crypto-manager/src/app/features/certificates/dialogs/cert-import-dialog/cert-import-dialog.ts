import { ChangeDetectionStrategy, Component, ElementRef, ViewChild, inject, input, output, signal } from '@angular/core';

import { ButtonModule } from 'primeng/button';
import { DialogModule } from 'primeng/dialog';
import { MessageModule } from 'primeng/message';
import { ProgressSpinner } from 'primeng/progressspinner';

import { CryptoService } from '../../../../core/services/crypto.service';
import { AppMessenger } from '../../../../core/services/app-messenger';
import { DownloadService } from '../../../../core/services/download.service';
import { ImportCertificateResponseDto } from '../../../../core/api/models';
import { CertificatesService } from '../../certificates.service';

@Component({
  selector: 'app-cert-import-dialog',
  imports: [
    DialogModule,
    ButtonModule,
    MessageModule,
    ProgressSpinner,
  ],
  templateUrl: './cert-import-dialog.html',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class CertImportDialog {
  private certificatesService = inject(CertificatesService);
  private cryptoService = inject(CryptoService);
  private downloadService = inject(DownloadService);
  private appMessenger = inject(AppMessenger);

  @ViewChild('certInput') certInput?: ElementRef<HTMLInputElement>;
  @ViewChild('chainInput') chainInput?: ElementRef<HTMLInputElement>;

  visible = input.required<boolean>();
  keyId = input.required<string>();
  visibleChange = output<boolean>();
  imported = output<ImportCertificateResponseDto>();

  loading = signal(false);
  error = signal<string | null>(null);
  certFile = signal<File | null>(null);
  chainFiles = signal<File[]>([]);

  close(): void {
    this.error.set(null);
    this.certFile.set(null);
    this.chainFiles.set([]);
    if (this.certInput) this.certInput.nativeElement.value = '';
    if (this.chainInput) this.chainInput.nativeElement.value = '';
    this.visibleChange.emit(false);
  }

  onCertFileSelected(event: Event): void {
    const input = event.target as HTMLInputElement;
    this.certFile.set(input.files?.[0] ?? null);
  }

  onChainFilesSelected(event: Event): void {
    const input = event.target as HTMLInputElement;
    this.chainFiles.set(Array.from(input.files ?? []));
  }

  removeChainFile(index: number): void {
    this.chainFiles.update(files => files.filter((_, i) => i !== index));
  }

  async submit(): Promise<void> {
    if (!this.keyId() || !this.certFile()) return;

    this.loading.set(true);
    this.error.set(null);

    try {
      const certificateDerBase64 = await this.fileToBase64Der(this.certFile()!);
      const chainDerBase64 = this.chainFiles().length
        ? await Promise.all(this.chainFiles().map(file => this.fileToBase64Der(file)))
        : null;

      this.certificatesService.importCertificate(this.keyId(), {
        certificateDerBase64,
        chainDerBase64,
      }).subscribe({
        next: result => {
          this.imported.emit(result);
          this.close();
          this.appMessenger.showMessage('success', 'Certificate imported');
        },
        error: err => {
          this.error.set('Failed to import certificate: ' + (err?.error?.Error || err?.message || 'Unknown error'));
          this.loading.set(false);
        },
        complete: () => {
          this.loading.set(false);
        }
      });
    } catch (err) {
      this.error.set(err instanceof Error ? err.message : 'Failed to read certificate files');
      this.loading.set(false);
    }
  }

  private async fileToBase64Der(file: File): Promise<string> {
    const text = await file.text();
    if (text.includes('-----BEGIN')) {
      return this.downloadService.bufferToBase64(this.cryptoService.pemToArrayBuffer(text));
    }

    return this.downloadService.bufferToBase64(await file.arrayBuffer());
  }
}
