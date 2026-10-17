import { inject, Injectable } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { firstValueFrom } from 'rxjs';
import { environment } from '../../../environments/environment';
import { LoginResponse } from '../models/auth.models';
import {
  decodeCreateOptions,
  decodeRequestOptions,
  encodeAttestation,
  encodeAssertion,
  PublicKeyCredentialCreationOptionsJSON,
  PublicKeyCredentialRequestOptionsJSON,
} from './webauthn-utils';

export interface CredentialView {
  id: string;
  nickname: string;
  createdAt: string;
  lastUsedAt: string | null;
  transports: string[];
  isBackedUp: boolean;
}

@Injectable({ providedIn: 'root' })
export class WebAuthnService {
  private readonly http = inject(HttpClient);
  private readonly base = environment.apiBaseUrl + '/api/auth/webauthn';

  async registerPasskey(attachment: 'platform' | 'cross-platform' | null, nickname?: string): Promise<void> {
    // Backend returns optionsJson as an opaque string (Fido2NetLib's ToJson()). Parse it client-side
    // before passing to the native API.
    const begin = await firstValueFrom(
      this.http.post<{sessionId: string; optionsJson: string}>(`${this.base}/register/begin`, { authenticatorAttachment: attachment })
    ).catch(e => { throw this.fromHttp(e); });
    const optionsRaw = JSON.parse(begin.optionsJson) as PublicKeyCredentialCreationOptionsJSON;
    const cred = await this.create(decodeCreateOptions(optionsRaw));
    await firstValueFrom(
      this.http.post(`${this.base}/register/complete`, { sessionId: begin.sessionId, attestationResponse: encodeAttestation(cred), nickname })
    ).catch(e => { throw this.fromHttp(e); });
  }

  async loginWithPasskey(userName?: string): Promise<LoginResponse> {
    const begin = await firstValueFrom(
      this.http.post<{sessionId: string; optionsJson: string}>(`${this.base}/login/begin`, { userName })
    ).catch(e => { throw this.fromHttp(e); });
    const optionsRaw = JSON.parse(begin.optionsJson) as PublicKeyCredentialRequestOptionsJSON;
    const cred = await this.get(decodeRequestOptions(optionsRaw));
    return firstValueFrom(
      this.http.post<LoginResponse>(`${this.base}/login/complete`, { sessionId: begin.sessionId, assertionResponse: encodeAssertion(cred) })
    ).catch(e => { throw this.fromHttp(e); });
  }

  // HttpErrorResponse (Angular) carries the server's response body in .error; extract the `error` field the
  // backend sets in BadRequest/Unauthorized({ error: "..." }) so callers see the server message, not Angular's
  // generic "Http failure response for ..." string. fromHttp() is called on every HTTP step so the error
  // surface is consistent regardless of which request leg fails.
  private fromHttp(e: unknown): Error {
    if (e != null && typeof e === 'object' && 'status' in e) {
      const body = (e as { error?: unknown }).error;
      if (body != null && typeof body === 'object' && 'error' in body && typeof (body as { error: unknown }).error === 'string')
        return new Error((body as { error: string }).error);
      return new Error(`Server error (${(e as { status: number }).status})`);
    }
    return e instanceof Error ? e : new Error(String(e));
  }

  // navigator.credentials.* throws DOMException on cancellation/timeout/abort — never returns null.
  // Translate the common DOMException names to a friendlier message for UI display.
  private async create(publicKey: PublicKeyCredentialCreationOptions): Promise<PublicKeyCredential> {
    try {
      const c = await navigator.credentials.create({ publicKey });
      if (!c) throw new Error('Passkey enrollment was cancelled.');
      return c as PublicKeyCredential;
    } catch (e) { throw this.toFriendly(e); }
  }

  private async get(publicKey: PublicKeyCredentialRequestOptions): Promise<PublicKeyCredential> {
    try {
      const c = await navigator.credentials.get({ publicKey });
      if (!c) throw new Error('Passkey sign-in was cancelled.');
      return c as PublicKeyCredential;
    } catch (e) { throw this.toFriendly(e); }
  }

  private toFriendly(e: unknown): Error {
    if (e instanceof DOMException) {
      if (e.name === 'NotAllowedError' || e.name === 'AbortError') return new Error('Cancelled.');
      if (e.name === 'InvalidStateError') return new Error('This authenticator is already registered.');
      return new Error(`${e.name}: ${e.message}`);
    }
    return e instanceof Error ? e : new Error(String(e));
  }

  list() { return this.http.get<CredentialView[]>(`${this.base}/credentials`); }
  rename(id: string, nickname: string) { return this.http.patch(`${this.base}/credentials/${id}`, { nickname }); }
  delete(id: string) { return this.http.delete(`${this.base}/credentials/${id}`); }

  static isSupported(): boolean {
    return typeof PublicKeyCredential !== 'undefined' && !!navigator.credentials?.create;
  }
}
