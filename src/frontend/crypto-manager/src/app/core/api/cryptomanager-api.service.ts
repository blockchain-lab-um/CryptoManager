import { Injectable, inject } from '@angular/core';
import { HttpClient, HttpParams } from '@angular/common/http';
import {
  CreateKeyRequestDto,
  CreateKeyResponseDto,
  GetPublicKeyResponseDto,
  ListKeysResponseDto,
  RotateKeyResponseDto,
  SignDigestRequestDto,
  SignDigestResponseDto
} from './models';
import { environment } from '../../../environments/environment';

@Injectable({ providedIn: 'root' })
export class CryptoManagerApi {
  private http = inject(HttpClient);
  private base = environment.apiBaseUrl;

  createKey(body: CreateKeyRequestDto) {
    return this.http.post<CreateKeyResponseDto>(`${this.base}/api/Keys`, body);
  }

  rotateKey(keyId: string) {
    return this.http.post<RotateKeyResponseDto>(`${this.base}/api/Keys/${encodeURIComponent(keyId)}/rotate`, {});
  }

  getPublicKey(keyId: string, version?: number | null) {
    let params = new HttpParams();
    if (version !== undefined && version !== null) params = params.set('version', String(version));
    return this.http.get<GetPublicKeyResponseDto>(`${this.base}/api/Keys/${encodeURIComponent(keyId)}/public`, { params });
  }

  signDigest(body: SignDigestRequestDto) {
    return this.http.post<SignDigestResponseDto>(`${this.base}/api/Crypto/sign`, body);
  }

  listKeys() {
    return this.http.get<ListKeysResponseDto>(`${this.base}/api/Keys`);
  }
}
