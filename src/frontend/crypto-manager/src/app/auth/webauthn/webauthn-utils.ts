// JSON-encoded shapes the backend sends (Fido2NetLib's options.ToJson() emits these).
// Byte fields arrive as base64url strings; decode* helpers convert them to ArrayBuffer
// before handing the options to navigator.credentials.create/get.

interface PublicKeyCredentialDescriptorJSON {
  type: PublicKeyCredentialType;
  id: string;
  transports?: AuthenticatorTransport[];
}

interface PublicKeyCredentialUserEntityJSON {
  id: string;
  name: string;
  displayName: string;
}

export interface PublicKeyCredentialCreationOptionsJSON {
  challenge: string;
  rp: PublicKeyCredentialRpEntity;
  user: PublicKeyCredentialUserEntityJSON;
  pubKeyCredParams: PublicKeyCredentialParameters[];
  timeout?: number;
  excludeCredentials?: PublicKeyCredentialDescriptorJSON[];
  authenticatorSelection?: AuthenticatorSelectionCriteria;
  attestation?: AttestationConveyancePreference;
  extensions?: AuthenticationExtensionsClientInputs;
}

export interface PublicKeyCredentialRequestOptionsJSON {
  challenge: string;
  timeout?: number;
  rpId?: string;
  allowCredentials?: PublicKeyCredentialDescriptorJSON[];
  userVerification?: UserVerificationRequirement;
  extensions?: AuthenticationExtensionsClientInputs;
}

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

export function decodeCreateOptions(o: PublicKeyCredentialCreationOptionsJSON): PublicKeyCredentialCreationOptions {
  return {
    ...o,
    challenge: base64UrlToBuffer(o.challenge),
    user: { ...o.user, id: base64UrlToBuffer(o.user.id) },
    excludeCredentials: (o.excludeCredentials ?? []).map(c => ({ ...c, id: base64UrlToBuffer(c.id) }))
  };
}

export function decodeRequestOptions(o: PublicKeyCredentialRequestOptionsJSON): PublicKeyCredentialRequestOptions {
  return {
    ...o,
    challenge: base64UrlToBuffer(o.challenge),
    allowCredentials: (o.allowCredentials ?? []).map(c => ({ ...c, id: base64UrlToBuffer(c.id) }))
  };
}

export interface EncodedAttestation {
  id: string;
  rawId: string;
  type: string;
  response: {
    attestationObject: string;
    clientDataJSON: string;
    transports: string[];
  };
}

export function encodeAttestation(c: PublicKeyCredential): EncodedAttestation {
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

export interface EncodedAssertion {
  id: string;
  rawId: string;
  type: string;
  response: {
    authenticatorData: string;
    clientDataJSON: string;
    signature: string;
    userHandle: string | null;
  };
}

export function encodeAssertion(c: PublicKeyCredential): EncodedAssertion {
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
