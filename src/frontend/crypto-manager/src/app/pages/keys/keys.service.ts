import { Injectable, inject } from '@angular/core';
import { rxResource } from '@angular/core/rxjs-interop';
import { map } from 'rxjs';
import { CryptoManagerApi } from '../../core/api/cryptomanager-api.service';

@Injectable({ providedIn: 'root' })
export class KeysService {
  private api = inject(CryptoManagerApi);

  keysResource = rxResource({
    stream: () => this.api.listKeys().pipe(map(res => res.keys ?? [])),
  });

  reload() {
    this.keysResource.reload();
  }

  stateSeverity(state?: string | null): 'success' | 'info' | 'warn' | 'danger' {
    const s = (state ?? '').toLowerCase();
    if (s.includes('active') || s.includes('ready')) return 'success';
    if (s.includes('pending')) return 'info';
    if (s.includes('disabled') || s.includes('inactive')) return 'warn';
    if (s.includes('revoked') || s.includes('deleted') || s.includes('error')) return 'danger';
    return 'info';
  }
}
