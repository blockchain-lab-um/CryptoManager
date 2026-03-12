import { Injectable } from '@angular/core';
import { sha256 } from '@noble/hashes/sha2.js';

@Injectable({ providedIn: 'root' })
export class CryptoService {
  async computeDigest(data: ArrayBuffer): Promise<ArrayBuffer> {
    if (crypto?.subtle) {
      return crypto.subtle.digest('SHA-256', data);
    }
    // Fallback for non-secure contexts (HTTP) — pure-JS implementation
    return sha256(new Uint8Array(data)).buffer as ArrayBuffer;
  }

  bufferToBase64(buffer: ArrayBuffer): string {
    const bytes = new Uint8Array(buffer);
    let binary = '';
    for (let i = 0; i < bytes.length; i++) {
      binary += String.fromCharCode(bytes[i]);
    }
    return btoa(binary);
  }

  downloadBuffer(buffer: ArrayBuffer, filename: string): void {
    const blob = new Blob([buffer], { type: 'application/octet-stream' });
    const url = URL.createObjectURL(blob);
    const a = document.createElement('a');
    a.href = url;
    a.download = filename;
    document.body.appendChild(a);
    a.click();
    document.body.removeChild(a);
    URL.revokeObjectURL(url);
  }

  downloadPem(pem: string, filename: string): void {
    const blob = new Blob([pem], { type: 'application/x-pem-file' });
    const url = URL.createObjectURL(blob);
    const a = document.createElement('a');
    a.href = url;
    a.download = filename;
    a.click();
    URL.revokeObjectURL(url);
  }
}
