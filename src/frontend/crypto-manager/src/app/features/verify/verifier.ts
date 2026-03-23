import { Injectable, inject } from '@angular/core';
import { firstValueFrom } from 'rxjs';

import { CryptoManagerApi } from '../../core/api/cryptomanager-api.service';
import { CryptoService } from '../../core/services/crypto.service';

export type PublicKeySource = 'select' | 'paste' | 'upload';

export interface VerifyResult {
  valid: boolean;
  mechanism: string;
}

interface AlgorithmSpec {
  importAlg: RsaHashedImportParams | EcKeyImportParams;
  verifyAlg: AlgorithmIdentifier;
  isDer: boolean;
  coordLen?: number;
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

@Injectable({ providedIn: 'root' })
export class Verifier {
  private api = inject(CryptoManagerApi);
  private cryptoService = inject(CryptoService);

  async resolvePem(
    source: PublicKeySource,
    keyId: string | null,
    pemText: string | null,
    pemFile: File | null,
  ): Promise<string> {
    if (source === 'select') {
      const res = await firstValueFrom(this.api.getPublicKey(keyId!));
      if (!res.publicKeyPem) throw new Error('No public key found for the selected key.');
      return res.publicKeyPem;
    }
    if (source === 'paste') {
      return pemText!.trim();
    }
    return pemFile!.text();
  }

  async verify(
    mechanism: string,
    dataBytes: ArrayBuffer,
    sigBuffer: ArrayBuffer,
    pem: string,
  ): Promise<boolean> {
    const spec = ALGORITHM_SPECS[mechanism];
    if (!spec) throw new Error(`Unsupported mechanism: ${mechanism}`);

    const spkiBuffer = this.cryptoService.pemToArrayBuffer(pem);

    if (crypto?.subtle) {
      const cryptoKey = await crypto.subtle.importKey(
        'spki', spkiBuffer, spec.importAlg, false, ['verify']
      );
      const sigForVerify = spec.isDer
        ? (this.derToP1363(new Uint8Array(sigBuffer), spec.coordLen!).buffer as ArrayBuffer)
        : sigBuffer;
      return crypto.subtle.verify(spec.verifyAlg, cryptoKey, sigForVerify, dataBytes);
    }

    return this.cryptoService.verifySignature(mechanism, dataBytes, sigBuffer, spkiBuffer);
  }

  // Converts an ASN.1 DER-encoded ECDSA signature to IEEE P1363 (raw r‖s) format
  // required by crypto.subtle.verify.
  private derToP1363(der: Uint8Array, coordLen: number): Uint8Array {
    if (der[0] !== 0x30) throw new Error('Invalid ECDSA signature: expected DER SEQUENCE (0x30)');
    let offset = 1;
    if (der[offset] & 0x80) {
      offset += 1 + (der[offset] & 0x7f);
    } else {
      offset += 1;
    }
    if (der[offset++] !== 0x02) throw new Error('Invalid DER: expected INTEGER tag for r');
    const rLen = der[offset++];
    const rPad = der[offset] === 0x00 ? 1 : 0;
    const rBytes = der.slice(offset + rPad, offset + rLen);
    offset += rLen;
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
