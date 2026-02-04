import { ChangeDetectionStrategy, Component, computed, inject, signal } from '@angular/core';
import { DecimalPipe } from '@angular/common';
import { FormBuilder, FormsModule, ReactiveFormsModule, Validators } from '@angular/forms';
import { InputNumberModule } from 'primeng/inputnumber';
import { ButtonModule } from 'primeng/button';
import { SelectModule } from 'primeng/select';
import { TextareaModule } from 'primeng/textarea';
import { MessageModule } from 'primeng/message';
import { SelectButtonModule } from 'primeng/selectbutton';
import { rxResource } from '@angular/core/rxjs-interop';
import { map } from 'rxjs';
import { CryptoManagerApi } from '../../../core/api/cryptomanager-api.service';
import { ActivatedRoute } from '@angular/router';
import { KeySummaryDto } from '../../../core/api/models';

type InputMode = 'text' | 'file';

@Component({
  imports: [
    DecimalPipe,
    FormsModule,
    ReactiveFormsModule,
    InputNumberModule,
    ButtonModule,
    TextareaModule,
    SelectModule,
    MessageModule,
    SelectButtonModule,
  ],
  templateUrl: './sign.component.html',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class SignComponent {
  private fb = inject(FormBuilder);
  private api = inject(CryptoManagerApi);
  private route = inject(ActivatedRoute);

  keysResource = rxResource({
    stream: () => this.api.listKeys().pipe(map(res => res.keys ?? [])),
  });

  keyOptions = computed(() => {
    const keys = this.keysResource.value() ?? [];
    return keys.map((k: KeySummaryDto) => ({
      label: k.name || k.keyId || 'Unnamed key',
      value: k.keyId,
    }));
  });

  mechanismOptions = [
    { label: 'CKM_RSA_PKCS', value: 'CKM_RSA_PKCS' },
    { label: 'CKM_RSA_PKCS_PSS', value: 'CKM_RSA_PKCS_PSS' },
    { label: 'CKM_ECDSA', value: 'CKM_ECDSA' },
    { label: 'RSA_PSS_SHA256', value: 'RSA_PSS_SHA256' },
  ];

  inputModeOptions = [
    { label: 'Text', value: 'text' as InputMode },
    { label: 'File', value: 'file' as InputMode },
  ];

  form = this.fb.group({
    keyId: ['', [Validators.required]],
    mechanism: ['', [Validators.required]],
    plainText: [''],
    version: this.fb.control<number | null>(null),
  });

  inputMode = signal<InputMode>('text');
  selectedFile = signal<File | null>(null);
  result = signal<any>(null);
  error = signal<string | null>(null);
  signing = signal(false);

  ngOnInit() {
    const keyId = this.route.snapshot.queryParamMap.get('keyId');
    if (keyId) this.form.patchValue({ keyId });
  }

  onFileSelected(event: Event) {
    const input = event.target as HTMLInputElement;
    const file = input.files?.[0] ?? null;
    this.selectedFile.set(file);
  }

  hasInput(): boolean {
    if (this.inputMode() === 'text') {
      return (this.form.getRawValue().plainText ?? '').trim().length > 0;
    }
    return this.selectedFile() !== null;
  }

  downloadBinarySignature() {
    if (this.result().signatureBase64) {
      const binaryString = atob(this.result().signatureBase64);
      const len = binaryString.length;
      const bytes = new Uint8Array(len);
      for (let i = 0; i < len; i++) {
        bytes[i] = binaryString.charCodeAt(i) & 0xff;
      }
      this.downloadBuffer(bytes.buffer, 'signature.bin');
    }
  }

  async downloadBinaryDigest() {
    const raw = await this.getInputBytes();

    if (!raw || raw.byteLength === 0) {
      this.error.set('No data to hash for download.');
      return;
    }

    const hashBuffer = await crypto.subtle.digest('SHA-256', raw);
    this.downloadBuffer(hashBuffer, 'digest.bin');
  }

  downloadBuffer(buffer: ArrayBuffer, filename: string) {
    const blob = new Blob([buffer], { type: 'application/octet-stream' });
    const url = URL.createObjectURL(blob);
    const a = document.createElement('a');
    a.href = url;
    a.download = filename;
    document.body.appendChild(a);
    a.click();
    document.body.removeChild(a);
    URL.revokeObjectURL(url);
  }

  async onSign() {
    this.error.set(null);
    this.result.set(null);

    if (this.form.invalid || !this.hasInput()) {
      if (!this.hasInput()) {
        this.error.set(this.inputMode() === 'text' ? 'Please enter text to sign.' : 'Please select a file to sign.');
      }
      return;
    }

    this.signing.set(true);

    try {
      const data = await this.getInputBytes();
      const hashBuffer = await crypto.subtle.digest('SHA-256', data);
      const digestBase64 = this.bufferToBase64(hashBuffer);

      const { keyId, mechanism, version } = this.form.getRawValue();

      this.api.signDigest({
        keyId: keyId!,
        mechanism: mechanism!,
        digestBase64,
        version,
      }).subscribe({
        next: (res) => {
          this.result.set(res);
          this.signing.set(false);
        },
        error: (e) => {
          this.error.set(e?.error?.message ?? 'Failed to sign');
          this.signing.set(false);
        },
      });
    } catch (e: any) {
      this.error.set(e?.message ?? 'Failed to process input');
      this.signing.set(false);
    }
  }

  private async getInputBytes(): Promise<ArrayBuffer> {
    if (this.inputMode() === 'file') {
      const file = this.selectedFile();
      if (!file) throw new Error('No file selected');
      return file.arrayBuffer();
    }
    const text = this.form.getRawValue().plainText ?? '';
    return new TextEncoder().encode(text).buffer as ArrayBuffer;
  }

  private bufferToBase64(buffer: ArrayBuffer): string {
    const bytes = new Uint8Array(buffer);
    let binary = '';
    for (let i = 0; i < bytes.length; i++) {
      binary += String.fromCharCode(bytes[i]);
    }
    return btoa(binary);
  }
}
