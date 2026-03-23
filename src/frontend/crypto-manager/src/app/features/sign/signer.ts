import { Injectable, inject } from '@angular/core';
import { Observable, from, map, switchMap } from 'rxjs';

import { CryptoManagerApi } from '../../core/api/cryptomanager-api.service';
import { CryptoService } from '../../core/services/crypto.service';
import { DownloadService } from '../../core/services/download.service';

export interface SignResult {
  format: 'signature' | 'file';
  signatureBase64?: string;
  signedFile?: File;
  auditEventId?: string;
  keyVersion?: number;
  encoding?: string;
}

@Injectable({ providedIn: 'root' })
export class Signer {
  private api = inject(CryptoManagerApi);
  private cryptoService = inject(CryptoService);
  private downloadService = inject(DownloadService);

  signFile(keyId: string, mechanism: string, file: File): Observable<SignResult> {
    return this.api.signFile({ keyId, mechanism, file }).pipe(
      map(res => ({
        format: 'file' as const,
        keyVersion: res.keyVersion,
        encoding: res.encoding ?? undefined,
        auditEventId: res.auditEventId ?? undefined,
        signedFile: res.signedFile,
      }))
    );
  }

  signDigest(keyId: string, mechanism: string, data: ArrayBuffer): Observable<SignResult> {
    return from(this.cryptoService.computeDigest(data)).pipe(
      map(hashBuffer => this.downloadService.bufferToBase64(hashBuffer)),
      switchMap(digestBase64 => this.api.signDigest({ keyId, mechanism, digestBase64 })),
      map(res => ({
        format: 'signature' as const,
        signatureBase64: res.signatureBase64 ?? undefined,
        auditEventId: res.auditEventId ?? undefined,
        keyVersion: res.keyVersion,
        encoding: res.encoding ?? undefined,
      }))
    );
  }
}
