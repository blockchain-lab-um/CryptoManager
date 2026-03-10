import { ChangeDetectionStrategy, Component, computed, inject, signal } from '@angular/core';
import { DecimalPipe } from '@angular/common';
import { FormBuilder, FormsModule, ReactiveFormsModule, Validators } from '@angular/forms';
import { ActivatedRoute } from '@angular/router';

import { ButtonModule } from 'primeng/button';
import { InputNumberModule } from 'primeng/inputnumber';
import { MessageModule } from 'primeng/message';
import { SelectButtonModule } from 'primeng/selectbutton';
import { SelectModule } from 'primeng/select';
import { TextareaModule } from 'primeng/textarea';

import { CryptoManagerApi } from '../../../core/api/cryptomanager-api.service';
import { CryptoService } from '../../../core/services/crypto.service';
import { KeysService } from '../../keys/keys.service';
import { KeySummaryDto } from '../../../core/api/models';
import { Card } from '../../../core/components/card/card';

type InputMode = 'text' | 'file';
type ReturnFormat = 'signature' | 'file';

export interface SignResult {
  format: ReturnFormat;
  signatureBase64?: string;
  signedFile?: File;
  auditEventId?: string;
  keyVersion?: number;
  encoding?: string;
}

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
    Card,
  ],
  templateUrl: './sign.component.html',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class SignComponent {

  private fb = inject(FormBuilder);
  private api = inject(CryptoManagerApi);
  private route = inject(ActivatedRoute);
  private cryptoService = inject(CryptoService);
  protected keysService = inject(KeysService);

  keyOptions = computed(() =>
    (this.keysService.keysResource.value() ?? []).map((k: KeySummaryDto) => ({
      label: k.name || k.keyId || 'Unnamed key',
      value: k.keyId,
    }))
  );

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

  returnFormatOptions = [
    { label: 'Signature only', value: 'signature' as ReturnFormat },
    { label: 'Signed file', value: 'file' as ReturnFormat },
  ];

  form = this.fb.group({
    keyId: ['', [Validators.required]],
    mechanism: ['', [Validators.required]],
    plainText: [''],
  });

  inputMode = signal<InputMode>('text');
  signatureReturnFormat = signal<ReturnFormat>('signature');
  selectedFile = signal<File | null>(null);
  result = signal<SignResult | null>(null);
  error = signal<string | null>(null);
  signing = signal(false);

  get signatureReturnFormatText(): string {
    return this.signatureReturnFormat() === 'signature' ? 'Signature' : 'Signed File';
  }

  ngOnInit() {
    const keyId = this.route.snapshot.queryParamMap.get('keyId');
    if (keyId) this.form.patchValue({ keyId });
  }

  onFileSelected(event: Event) {
    const input = event.target as HTMLInputElement;
    this.selectedFile.set(input.files?.[0] ?? null);
  }

  hasInput(): boolean {
    if (this.inputMode() === 'text') {
      return (this.form.getRawValue().plainText ?? '').trim().length > 0;
    }
    return this.selectedFile() !== null;
  }

  async downloadBinarySignature() {
    const sig = this.result()?.signatureBase64;
    if (!sig) return;

    const binaryString = atob(sig);
    const bytes = new Uint8Array(binaryString.length);
    for (let i = 0; i < binaryString.length; i++) {
      bytes[i] = binaryString.charCodeAt(i) & 0xff;
    }
    this.cryptoService.downloadBuffer(bytes.buffer as ArrayBuffer, 'signature.bin');
  }

  async downloadSignedFile() {
    const file = this.result()?.signedFile;
    if (!file) return;
    this.cryptoService.downloadBuffer(await file.arrayBuffer(), file.name);
  }

  async downloadBinaryDigest() {
    const raw = await this.getInputBytes();
    if (!raw || raw.byteLength === 0) {
      this.error.set('No data to hash for download.');
      return;
    }
    const hashBuffer = await this.cryptoService.computeDigest(raw);
    this.cryptoService.downloadBuffer(hashBuffer, 'digest.bin');
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
      const { keyId, mechanism } = this.form.getRawValue();

      if (this.signatureReturnFormat() === 'file') {
        const file = this.selectedFile();
        if (!file) throw new Error('No file selected for signing');
        this.api.signFile({ keyId: keyId!, mechanism: mechanism!, file: file }).subscribe({
          next: (res) => {
            this.result.set({
              format: 'file',
              keyVersion: res.keyVersion,
              encoding: res.encoding ?? undefined,
              auditEventId: res.auditEventId ?? undefined,
              signedFile: res.signedFile
            }); this.signing.set(false);
          },
          error: (e) => { this.error.set(e?.error?.message ?? 'Failed to sign'); this.signing.set(false); },
        });
      } else {
        const hashBuffer = await this.cryptoService.computeDigest(data);
        const digestBase64 = this.cryptoService.bufferToBase64(hashBuffer);

        this.api.signDigest({ keyId: keyId!, mechanism: mechanism!, digestBase64 }).subscribe({
          next: (res) => {
            this.result.set({
              format: 'signature',
              signatureBase64: res.signatureBase64 ?? undefined,
              auditEventId: res.auditEventId ?? undefined,
              keyVersion: res.keyVersion,
              encoding: res.encoding ?? undefined,
            }); this.signing.set(false);
          },
          error: (e) => { this.error.set(e?.error?.message ?? 'Failed to sign'); this.signing.set(false); },
        });
      }
    } catch (e: unknown) {
      this.error.set(e instanceof Error ? e.message : 'Failed to process input');
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
}
