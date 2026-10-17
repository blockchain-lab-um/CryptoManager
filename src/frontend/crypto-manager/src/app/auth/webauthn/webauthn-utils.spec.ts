import { describe, it, expect } from 'vitest';
import { bufferToBase64Url, base64UrlToBuffer } from './webauthn-utils';

describe('webauthn-utils base64url', () => {
  it('round-trips edge bytes including 0x00 and 0xFF', () => {
    const src = new Uint8Array([0, 1, 2, 3, 253, 254, 255]);
    const back = new Uint8Array(base64UrlToBuffer(bufferToBase64Url(src.buffer)));
    expect(Array.from(back)).toEqual(Array.from(src));
  });

  it('handles inputs whose length requires padding to be stripped', () => {
    // 5 bytes → base64 length 8 with 1 '=' which must be stripped in base64url
    const src = new Uint8Array([10, 20, 30, 40, 50]);
    const encoded = bufferToBase64Url(src.buffer);
    expect(encoded).not.toContain('=');
    const back = new Uint8Array(base64UrlToBuffer(encoded));
    expect(Array.from(back)).toEqual(Array.from(src));
  });
});