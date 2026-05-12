export function bufferToBase64Url(buf: ArrayBuffer): string {
  const bytes = new Uint8Array(buf);
  let s = '';
  for (const b of bytes) s += String.fromCharCode(b);
  return btoa(s).replace(/\+/g, '-').replace(/\//g, '_').replace(/=+$/, '');
}

export function base64UrlToBuffer(s: string): ArrayBuffer {
  const pad = s.length % 4 === 0 ? '' : '='.repeat(4 - (s.length % 4));
  const raw = atob(s.replace(/-/g, '+').replace(/_/g, '/') + pad);
  const out = new Uint8Array(raw.length);
  for (let i = 0; i < raw.length; i++) out[i] = raw.charCodeAt(i);
  return out.buffer;
}

export function decodeCreateOptions(o: any): PublicKeyCredentialCreationOptions {
  return {
    ...o,
    challenge: base64UrlToBuffer(o.challenge),
    user: { ...o.user, id: base64UrlToBuffer(o.user.id) },
    excludeCredentials: (o.excludeCredentials ?? []).map((c: any) => ({ ...c, id: base64UrlToBuffer(c.id) }))
  };
}

export function decodeRequestOptions(o: any): PublicKeyCredentialRequestOptions {
  return {
    ...o,
    challenge: base64UrlToBuffer(o.challenge),
    allowCredentials: (o.allowCredentials ?? []).map((c: any) => ({ ...c, id: base64UrlToBuffer(c.id) }))
  };
}

export function encodeAttestation(c: PublicKeyCredential) {
  const r = c.response as AuthenticatorAttestationResponse;
  // getTransports() returns lowercase WebAuthn transport strings ("usb"|"nfc"|"ble"|"internal"|"hybrid").
  // Pass them through unchanged — the backend stores these strings verbatim, so frontend filtering by transport works.
  const transports: string[] = (r as { getTransports?: () => string[] }).getTransports?.() ?? [];
  return {
    id: c.id,
    rawId: bufferToBase64Url(c.rawId),
    type: c.type,
    response: {
      attestationObject: bufferToBase64Url(r.attestationObject),
      clientDataJSON: bufferToBase64Url(r.clientDataJSON),
      transports
    }
  };
}

export function encodeAssertion(c: PublicKeyCredential) {
  const r = c.response as AuthenticatorAssertionResponse;
  return {
    id: c.id,
    rawId: bufferToBase64Url(c.rawId),
    type: c.type,
    response: {
      authenticatorData: bufferToBase64Url(r.authenticatorData),
      clientDataJSON: bufferToBase64Url(r.clientDataJSON),
      signature: bufferToBase64Url(r.signature),
      userHandle: r.userHandle ? bufferToBase64Url(r.userHandle) : null
    }
  };
}