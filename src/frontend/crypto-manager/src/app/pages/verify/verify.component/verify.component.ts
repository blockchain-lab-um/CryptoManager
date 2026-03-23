import { ChangeDetectionStrategy, Component, computed, inject, signal } from '@angular/core';
import { DecimalPipe } from '@angular/common';
import { FormBuilder, FormsModule, ReactiveFormsModule, Validators } from '@angular/forms';
import { firstValueFrom } from 'rxjs';

import { ButtonModule } from 'primeng/button';
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
type PublicKeySource = 'select' | 'paste' | 'upload';

interface AlgorithmSpec {
  importAlg: RsaHashedImportParams | EcKeyImportParams;
  verifyAlg: AlgorithmIdentifier;
  isDer: boolean;
  coordLen?: number;
}

interface VerifyResult {
  valid: boolean;
  mechanism: string;
}

const ALGORITHM_SPECS: Record<string, AlgorithmSpec> = {
  RSA_PSS_SHA256: {
    importAlg: { name: 'RSA-PSS', hash: 'SHA-256' },
    verifyAlg: { name: 'RSA-PSS', saltLength: 32 } as RsaPssParams,
    isDer: false,
  },
  ECDSA_P256_SHA256: {
    importAlg: { name: 'ECDSA', namedCurve: 'P-256' },
    verifyAlg: { name: 'ECDSA', hash: 'SHA-256' } as EcdsaParams,
    isDer: true,
    coordLen: 32,
  },
};

@Component({
  imports: [
    DecimalPipe,
    FormsModule,
    ReactiveFormsModule,
    ButtonModule,
    TextareaModule,
    SelectModule,
    MessageModule,
    SelectButtonModule,
    Card,
  ],
  templateUrl: './verify.component.html',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class VerifyComponent {
  private fb = inject(FormBuilder);
  private api = inject(CryptoManagerApi);
  private cryptoService = inject(CryptoService);
  protected keysService = inject(KeysService);

  keyOptions = computed(() =>
    (this.keysService.keysResource.value() ?? []).map((k: KeySummaryDto) => ({
      label: k.name || k.keyId || 'Unnamed key',
      value: k.keyId,
    }))
  );

  mechanismOptions = [
    { label: 'RSA-PSS (SHA-256)', value: 'RSA_PSS_SHA256' },
    { label: 'ECDSA P-256 (SHA-256)', value: 'ECDSA_P256_SHA256' },
  ];

  inputModeOptions = [
    { label: 'Text', value: 'text' as InputMode },
    { label: 'File', value: 'file' as InputMode },
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
  publicKeySource = signal<PublicKeySource>('select');
  selectedDataFile = signal<File | null>(null);
  selectedSignatureFile = signal<File | null>(null);
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
      const { mechanism, keyId, pemText } = this.form.getRawValue();
      const spec = ALGORITHM_SPECS[mechanism!];
      if (!spec) throw new Error(`Unsupported mechanism: ${mechanism}`);

      const pem = await this.resolvePem(keyId, pemText);
      const spkiBuffer = this.cryptoService.pemToArrayBuffer(pem);
      const dataBytes = await this.getInputBytes();
      const sigBuffer = await this.selectedSignatureFile()!.arrayBuffer();

      let valid: boolean;
      if (crypto?.subtle) {
        const cryptoKey = await crypto.subtle.importKey(
          'spki', spkiBuffer, spec.importAlg, false, ['verify']
        );
        const sigForVerify = spec.isDer
          ? (this.derToP1363(new Uint8Array(sigBuffer), spec.coordLen!).buffer as ArrayBuffer)
          : sigBuffer;
        valid = await crypto.subtle.verify(spec.verifyAlg, cryptoKey, sigForVerify, dataBytes);
      } else {
        valid = await this.cryptoService.verifySignature(mechanism!, dataBytes, sigBuffer, spkiBuffer);
      }

      this.result.set({ valid, mechanism: mechanism! });
    } catch (e: unknown) {
      this.error.set(e instanceof Error ? e.message : 'Verification failed');
    } finally {
      this.verifying.set(false);
    }
  }

  private async resolvePem(keyId: string | null, pemText: string | null): Promise<string> {
    const source = this.publicKeySource();
    if (source === 'select') {
      const res = await firstValueFrom(this.api.getPublicKey(keyId!));
      if (!res.publicKeyPem) throw new Error('No public key found for the selected key.');
      return res.publicKeyPem;
    }
    if (source === 'paste') {
      return pemText!.trim();
    }
    return this.selectedPemFile()!.text();
  }

  private async getInputBytes(): Promise<ArrayBuffer> {
    if (this.inputMode() === 'file') {
      return this.selectedDataFile()!.arrayBuffer();
    }
    const text = this.form.getRawValue().plainText ?? '';
    return new TextEncoder().encode(text).buffer as ArrayBuffer;
  }

  private hasRequiredInputs(): boolean {
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

  // Converts an ASN.1 DER-encoded ECDSA signature to IEEE P1363 (raw r‖s) format
  // required by crypto.subtle.verify.
  private derToP1363(der: Uint8Array, coordLen: number): Uint8Array {
    if (der[0] !== 0x30) throw new Error('Invalid ECDSA signature: expected DER SEQUENCE (0x30)');
    let offset = 1;
    // Skip SEQUENCE length (handle multi-byte length encoding)
    if (der[offset] & 0x80) {
      offset += 1 + (der[offset] & 0x7f);
    } else {
      offset += 1;
    }
    // Parse r INTEGER
    if (der[offset++] !== 0x02) throw new Error('Invalid DER: expected INTEGER tag for r');
    const rLen = der[offset++];
    const rPad = der[offset] === 0x00 ? 1 : 0;
    const rBytes = der.slice(offset + rPad, offset + rLen);
    offset += rLen;
    // Parse s INTEGER
    if (der[offset++] !== 0x02) throw new Error('Invalid DER: expected INTEGER tag for s');
    const sLen = der[offset++];
    const sPad = der[offset] === 0x00 ? 1 : 0;
    const sBytes = der.slice(offset + sPad, offset + sLen);

    const p1363 = new Uint8Array(coordLen * 2);
    p1363.set(rBytes, coordLen - rBytes.length);
    p1363.set(sBytes, coordLen * 2 - sBytes.length);
    return p1363;
  }
}
