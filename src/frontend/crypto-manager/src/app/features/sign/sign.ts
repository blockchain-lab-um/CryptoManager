import { ChangeDetectionStrategy, Component, computed, inject, signal } from '@angular/core';
import { rxResource, toSignal } from '@angular/core/rxjs-interop';
import { DecimalPipe } from '@angular/common';
import { FormBuilder, FormsModule, ReactiveFormsModule, Validators } from '@angular/forms';
import { ActivatedRoute, RouterLink } from '@angular/router';
import { catchError, of, startWith, throwError } from 'rxjs';

import { ButtonModule } from 'primeng/button';
import { InputNumberModule } from 'primeng/inputnumber';
import { MessageModule } from 'primeng/message';
import { SelectButtonModule } from 'primeng/selectbutton';
import { SelectModule } from 'primeng/select';
import { TextareaModule } from 'primeng/textarea';

import { CryptoManagerApi } from '../../core/api/cryptomanager-api.service';
import { CryptoService } from '../../core/services/crypto.service';
import { DownloadService } from '../../core/services/download.service';
import { KeysService } from '../keys/keys.service';
import { Card } from '../../shared/components/card/card';
import { toKeySelectOptions } from '../keys/keys.utils';
import { Signer, SignResult } from './signer';
import { StampPosition } from '../../core/api/models';
import { StampPositionDialog } from './stamp-position-dialog';

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
    RouterLink,
    StampPositionDialog,
  ],
  templateUrl: './sign.html',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class SignPage {
  private fb = inject(FormBuilder);
  private route = inject(ActivatedRoute);
  private api = inject(CryptoManagerApi);
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
  isPdf = signal(false);
  addStamp = signal(true);
  stampPosition = signal<StampPosition | null>(null);
  stampDialogVisible = signal(false);
  result = signal<SignResult | null>(null);
  error = signal<string | null>(null);
  signing = signal(false);
  readonly selectedKeyId = toSignal(
    this.form.controls.keyId.valueChanges.pipe(startWith(this.form.controls.keyId.value)),
    { initialValue: this.form.controls.keyId.value }
  );
  readonly fileSigningSelected = computed(() => this.inputMode() === 'file');
  readonly activeCertResource = rxResource({
    params: () => {
      const keyId = this.selectedKeyId();
      return this.fileSigningSelected() && keyId ? keyId : undefined;
    },
    stream: ({ params: keyId }) =>
      this.api.getActiveCertificate(keyId).pipe(
        catchError(err => err.status === 404 ? of(null) : throwError(() => err))
      ),
  });
  readonly activeCertMissing = computed(() => {
    const keyId = this.selectedKeyId();

    return (
      this.fileSigningSelected() && this.signatureReturnFormat() === 'file' &&
      !!keyId &&
      !this.activeCertResource.isLoading() &&
      this.activeCertResource.value() === null
    );
  });

  get signatureReturnFormatText(): string {
    return this.signatureReturnFormat() === 'signature' ? 'Signature' : 'Signed File';
  }

  ngOnInit() {
    const keyId = this.route.snapshot.queryParamMap.get('keyId');
    if (keyId) this.form.patchValue({ keyId });
  }

  async onFileSelected(event: Event) {
    const input = event.target as HTMLInputElement;
    const file = input.files?.[0] ?? null;
    this.selectedFile.set(file);
    this.stampPosition.set(null);

    if (file) {
      const bytes = new Uint8Array(await file.slice(0, 5).arrayBuffer());
      const magic =
        bytes[0] === 0x25 &&
        bytes[1] === 0x50 &&
        bytes[2] === 0x44 &&
        bytes[3] === 0x46 &&
        bytes[4] === 0x2d;
      this.isPdf.set(magic);
    } else {
      this.isPdf.set(false);
    }
  }

  onStampPositionApplied(pos: StampPosition | null) {
    if (pos !== null) {
      this.stampPosition.set(pos);
    }
    this.stampDialogVisible.set(false);
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

    if (this.fileSigningSelected() && this.activeCertMissing()) {
      this.error.set('This key has no active certificate. Signing is not available.');
      return;
    }

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

      const source$ = this.inputMode() === 'file' && this.signatureReturnFormat() === 'file'
        ? this.signer.signFile(keyId!, mechanism!, this.selectedFile()!, {
            addStamp: this.isPdf() ? this.addStamp() : false,
            position: this.stampPosition(),
          })
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
