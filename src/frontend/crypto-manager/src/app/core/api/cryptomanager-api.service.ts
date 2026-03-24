import { Injectable, inject } from '@angular/core';
import { HttpClient, HttpParams, HttpResponse } from '@angular/common/http';
import {
  AuditLogQueryParams,
  CreateKeyRequestDto,
  CreateKeyResponseDto,
  GetPublicKeyResponseDto,
  ListKeysResponseDto,
  PagedAuditLogResultDto,
  RotateKeyResponseDto,
  SignDigestRequestDto,
  SignDigestResponseDto,
  SignFileRequestDto,
  SignFileResponseDto
} from './models';
import { environment } from '../../../environments/environment';
import { map } from 'rxjs';

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

  deleteKey(keyId: string) {
    return this.http.delete(`${this.base}/api/Keys/${encodeURIComponent(keyId)}`);
  }

  signFile(dto: SignFileRequestDto) {
    const formData = new FormData();
    formData.append('keyId', dto.keyId);
    formData.append('mechanism', dto.mechanism);
    formData.append('file', dto.file);

    return this.http.post(`${this.base}/api/Crypto/sign-file`, formData, {
      responseType: 'blob',
      observe: 'response'
    }).pipe(
      map((response: HttpResponse<Blob>): SignFileResponseDto => ({
        auditEventId: response.headers.get('X-Audit-Event-Id'),
        keyId: response.headers.get('X-Key-Id'),
        keyVersion: Number(response.headers.get('X-Key-Version')),
        mechanism: response.headers.get('X-Mechanism'),
        signedFormat: response.headers.get('X-Signed-Format'),
        signedFile: new File([response.body!], response.headers.get('X-File-Name') ?? 'signedfile', { type: response.body!.type }),
      }))
    );
  }

  listKeys() {
    return this.http.get<ListKeysResponseDto>(`${this.base}/api/Keys`);
  }

  getAuditLog(params: AuditLogQueryParams) {
    let p = new HttpParams()
      .set('page', String(params.page))
      .set('pageSize', String(params.pageSize));
    if (params.from)   p = p.set('from', params.from);
    if (params.to)     p = p.set('to', params.to);
    if (params.action) p = p.set('action', params.action);
    if (params.keyId)  p = p.set('keyId', params.keyId);
    if (params.actor)  p = p.set('actor', params.actor);
    return this.http.get<PagedAuditLogResultDto>(`${this.base}/api/Audit/logs`, { params: p });
  }
}
