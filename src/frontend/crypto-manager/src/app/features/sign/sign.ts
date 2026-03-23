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

import { CryptoService } from '../../core/services/crypto.service';
import { DownloadService } from '../../core/services/download.service';
import { KeysService } from '../keys/keys.service';
import { Card } from '../../shared/components/card/card';
import { toKeySelectOptions } from '../keys/keys.utils';
import { Signer, SignResult } from './signer';

type InputMode = 'text' | 'file';
type ReturnFormat = 'signature' | 'file';

export type { SignResult };

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
  templateUrl: './sign.html',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class SignPage {
  private fb = inject(FormBuilder);
  private route = inject(ActivatedRoute);
  private signer = inject(Signer);
  private cryptoService = inject(CryptoService);
  private downloadService = inject(DownloadService);
  protected keysService = inject(KeysService);

  keyOptions = computed(() => toKeySelectOptions(this.keysService.keysResource.value() ?? []));

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
    this.downloadService.downloadBuffer(bytes.buffer as ArrayBuffer, 'signature.bin');
  }

  async downloadBinaryDigest() {
    const raw = await this.getInputBytes();
    if (!raw || raw.byteLength === 0) {
      this.error.set('No data to hash for download.');
      return;
    }
    const hashBuffer = await this.cryptoService.computeDigest(raw);
    this.downloadService.downloadBuffer(hashBuffer, 'digest.bin');
  }

  async downloadSignedFile() {
    const file = this.result()?.signedFile;
    if (!file) return;
    this.downloadService.downloadBuffer(await file.arrayBuffer(), file.name);
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

    const { keyId, mechanism } = this.form.getRawValue();

    try {
      const data = await this.getInputBytes();

      const source$ = this.signatureReturnFormat() === 'file'
        ? this.signer.signFile(keyId!, mechanism!, this.selectedFile()!)
        : this.signer.signDigest(keyId!, mechanism!, data);

      source$.subscribe({
        next: (res) => { this.result.set(res); this.signing.set(false); },
        error: (e) => { this.error.set(e?.error?.message ?? 'Failed to sign'); this.signing.set(false); },
      });
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
