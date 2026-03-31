import { ChangeDetectionStrategy, Component, computed, inject, signal } from '@angular/core';
import { DatePipe, DecimalPipe } from '@angular/common';
import { FormBuilder, FormsModule, ReactiveFormsModule, Validators } from '@angular/forms';

import { ButtonModule } from 'primeng/button';
import { MessageModule } from 'primeng/message';
import { SelectButtonModule } from 'primeng/selectbutton';
import { SelectModule } from 'primeng/select';
import { TextareaModule } from 'primeng/textarea';

import { KeysService } from '../keys/keys.service';
import { Card } from '../../shared/components/card/card';
import { toKeySelectOptions } from '../keys/keys.utils';
import { Verifier, VerifyResult, PublicKeySource, VerifyMode } from './verifier';

type InputMode = 'text' | 'file';

@Component({
  imports: [
    DecimalPipe,
    DatePipe,
    FormsModule,
    ReactiveFormsModule,
    ButtonModule,
    TextareaModule,
    SelectModule,
    MessageModule,
    SelectButtonModule,
    Card,
  ],
  templateUrl: './verify.html',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class VerifyPage {
  private fb = inject(FormBuilder);
  private verifier = inject(Verifier);
  protected keysService = inject(KeysService);

  keyOptions = computed(() => toKeySelectOptions(this.keysService.keysResource.value() ?? []));

  mechanismOptions = [
    { label: 'RSA-PSS (SHA-256)', value: 'RSA_PSS_SHA256' },
    { label: 'ECDSA P-256 (SHA-256)', value: 'ECDSA_P256_SHA256' },
  ];

  inputModeOptions = [
    { label: 'Text', value: 'text' as InputMode },
    { label: 'File', value: 'file' as InputMode },
  ];

  verifyModeOptions = [
    { label: 'Signature file', value: 'signature-file' as VerifyMode },
    { label: 'Signed file', value: 'signed-file' as VerifyMode },
  ];

  publicKeySourceOptions = [
    { label: 'Select key', value: 'select' as PublicKeySource },
    { label: 'Paste PEM', value: 'paste' as PublicKeySource },
    { label: 'Upload .pem', value: 'upload' as PublicKeySource },
  ];

  form = this.fb.group({
    mechanism: ['', Validators.required],
    plainText: [''],
    keyId: [''],
    pemText: [''],
  });

  inputMode = signal<InputMode>('text');
  verifyMode = signal<VerifyMode>('signature-file');
  publicKeySource = signal<PublicKeySource>('select');
  selectedDataFile = signal<File | null>(null);
  selectedSignatureFile = signal<File | null>(null);
  selectedSignedFile = signal<File | null>(null);
  selectedPemFile = signal<File | null>(null);
  verifying = signal(false);
  result = signal<VerifyResult | null>(null);
  error = signal<string | null>(null);

  onDataFileSelected(event: Event) {
    const input = event.target as HTMLInputElement;
    this.selectedDataFile.set(input.files?.[0] ?? null);
  }

  onSignatureFileSelected(event: Event) {
    const input = event.target as HTMLInputElement;
    this.selectedSignatureFile.set(input.files?.[0] ?? null);
  }

  onSignedFileSelected(event: Event) {
    const input = event.target as HTMLInputElement;
    this.selectedSignedFile.set(input.files?.[0] ?? null);
  }

  onPemFileSelected(event: Event) {
    const input = event.target as HTMLInputElement;
    this.selectedPemFile.set(input.files?.[0] ?? null);
  }

  async onVerify() {
    this.error.set(null);
    this.result.set(null);

    if (!this.hasRequiredInputs()) {
      this.error.set('Please fill in all required fields.');
      return;
    }

    this.verifying.set(true);

    try {
      if (this.verifyMode() === 'signed-file') {
        this.result.set(await this.verifier.verifySignedFile(this.selectedSignedFile()!));
        return;
      }

      const { mechanism, keyId, pemText } = this.form.getRawValue();
      const pem = await this.verifier.resolvePem(
        this.publicKeySource(), keyId, pemText, this.selectedPemFile()
      );
      const dataBytes = await this.getInputBytes();
      const sigBuffer = await this.selectedSignatureFile()!.arrayBuffer();

      const valid = await this.verifier.verify(mechanism!, dataBytes, sigBuffer, pem);
      this.result.set({ valid, mode: 'signature-file', mechanism: mechanism! });
    } catch (e: unknown) {
      this.error.set(e instanceof Error ? e.message : 'Verification failed');
    } finally {
      this.verifying.set(false);
    }
  }

  private async getInputBytes(): Promise<ArrayBuffer> {
    if (this.inputMode() === 'file') {
      return this.selectedDataFile()!.arrayBuffer();
    }
    const text = this.form.getRawValue().plainText ?? '';
    return new TextEncoder().encode(text).buffer as ArrayBuffer;
  }

  private hasRequiredInputs(): boolean {
    if (this.verifyMode() === 'signed-file') return !!this.selectedSignedFile();

    const { mechanism, keyId, pemText, plainText } = this.form.getRawValue();
    if (!mechanism) return false;
    if (this.inputMode() === 'text' && !(plainText ?? '').trim()) return false;
    if (this.inputMode() === 'file' && !this.selectedDataFile()) return false;
    if (!this.selectedSignatureFile()) return false;
    const source = this.publicKeySource();
    if (source === 'select' && !keyId) return false;
    if (source === 'paste' && !(pemText ?? '').trim()) return false;
    if (source === 'upload' && !this.selectedPemFile()) return false;
    return true;
  }
}
