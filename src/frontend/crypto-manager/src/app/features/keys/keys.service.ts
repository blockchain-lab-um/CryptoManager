import { Injectable, inject } from '@angular/core';
import { rxResource } from '@angular/core/rxjs-interop';
import { map, tap } from 'rxjs';
import { CryptoManagerApi } from '../../core/api/cryptomanager-api.service';
import { CreateKeyRequestDto } from '../../core/api/models';

@Injectable({ providedIn: 'root' })
export class KeysService {
  private api = inject(CryptoManagerApi);

  readonly keysResource = rxResource({
    stream: () => this.api.listKeys().pipe(
      map(res => res.keys ?? [])
    ),
  });

  reload() {
    this.keysResource.reload();
  }

  createKey(body: CreateKeyRequestDto) {
    return this.api.createKey(body).pipe(
      tap(() => this.reload())
    );
  }

  deleteKey(keyId: string) {
    return this.api.deleteKey(keyId).pipe(
      tap(() => this.reload())
    );
  }

  rotateKey(keyId: string) {
    return this.api.rotateKey(keyId).pipe(
      tap(() => this.reload())
    );
  }

  getPublicKey(keyId: string, version?: number | null) {
    return this.api.getPublicKey(keyId, version);
  }

}
