import { Injectable } from '@angular/core';
import { sha256 } from '@noble/hashes/sha2.js';
import { p256 } from '@noble/curves/nist.js';


@Injectable({ providedIn: 'root' })
export class CryptoService {
  async computeDigest(data: ArrayBuffer): Promise<ArrayBuffer> {
    if (crypto?.subtle) {
      return crypto.subtle.digest('SHA-256', data);
    }
    // Fallback for non-secure contexts (HTTP) — pure-JS implementation
    return sha256(new Uint8Array(data)).buffer as ArrayBuffer;
  }

  pemToArrayBuffer(pem: string): ArrayBuffer {
    const b64 = pem
      .replace(/-----BEGIN [^-]+-----/g, '')
      .replace(/-----END [^-]+-----/g, '')
      .replace(/\s/g, '');
    const binary = atob(b64);
    const bytes = new Uint8Array(binary.length);
    for (let i = 0; i < binary.length; i++) {
      bytes[i] = binary.charCodeAt(i);
    }
    return bytes.buffer as ArrayBuffer;
  }

  /**
   * Pure-JS signature verification fallback for non-secure contexts (HTTP).
   * ECDSA P-256 uses @noble/curves; RSA-PSS uses BigInt modular exponentiation.
   * The sig parameter must be the raw file bytes (DER-encoded for ECDSA, raw for RSA-PSS).
   */
  async verifySignature(
    mechanism: string,
    data: ArrayBuffer,
    sig: ArrayBuffer,
    spki: ArrayBuffer,
  ): Promise<boolean> {
    switch (mechanism) {
      case 'ECDSA_P256_SHA256':
        return this.verifyEcdsaP256(data, sig, spki);
      case 'RSA_PSS_SHA256':
        return this.verifyRsaPssSha256(data, sig, spki);
      default:
        throw new Error(`Unsupported mechanism for non-secure context: ${mechanism}`);
    }
  }

  private async verifyEcdsaP256(
    data: ArrayBuffer,
    sig: ArrayBuffer,
    spki: ArrayBuffer,
  ): Promise<boolean> {
    const msgHash = sha256(new Uint8Array(data));
    const pubKey = this.extractEcPoint(new Uint8Array(spki));
    try {
      const der = new Uint8Array(sig);
      // Parse DER SEQUENCE { INTEGER r, INTEGER s }
      let i = 2; // skip 0x30 <len>
      if (der[1] & 0x80) i += der[1] & 0x7f; // multi-byte length
      i++; // skip 0x02
      const rLen = der[i++];
      const rPad = der[i] === 0 ? 1 : 0;
      const r = this.bytesToBigInt(der.slice(i + rPad, i + rLen));
      i += rLen;
      i++; // skip 0x02
      const sLen = der[i++];
      const sPad = der[i] === 0 ? 1 : 0;
      const s = this.bytesToBigInt(der.slice(i + sPad, i + sLen));
      // P1363: r and s each padded to 32 bytes
      const p1363 = new Uint8Array(64);
      p1363.set(this.bigIntToBytes(r, 32), 0);
      p1363.set(this.bigIntToBytes(s, 32), 32);
      return p256.verify(p1363, msgHash, pubKey);
    } catch {
      return false;
    }
  }

  /** Extracts the raw EC point bytes from a SubjectPublicKeyInfo (SPKI) DER buffer. */
  private extractEcPoint(spki: Uint8Array): Uint8Array {
    const readLen = (offset: number): [number, number] => {
      if (spki[offset] < 0x80) return [spki[offset], 1];
      const n = spki[offset] & 0x7f;
      let len = 0;
      for (let j = 0; j < n; j++) len = (len << 8) | spki[offset + 1 + j];
      return [len, 1 + n];
    };
    let i = 1; // skip outer SEQUENCE tag
    const [, outerLB] = readLen(i); i += outerLB;
    i++; // skip AlgorithmIdentifier SEQUENCE tag
    const [algLen, algLB] = readLen(i); i += algLB + algLen;
    i++; // skip BIT STRING tag
    const [, bsLB] = readLen(i); i += bsLB;
    i++; // skip unused-bits byte (0x00)
    return spki.slice(i);
  }

  /**
   * RSA-PSS-SHA256 verification per RFC 8017 §9.1.2 using BigInt.
   * Assumes saltLength === 32 (= hLen for SHA-256), which matches the backend default.
   */
  private async verifyRsaPssSha256(
    data: ArrayBuffer,
    sig: ArrayBuffer,
    spki: ArrayBuffer,
  ): Promise<boolean> {
    const { n, e, modLen } = this.parseRsaSpki(new Uint8Array(spki));

    const s = this.bytesToBigInt(new Uint8Array(sig));
    const m = this.modPow(s, e, n);
    const em = this.bigIntToBytes(m, modLen);

    const hLen = 32; // SHA-256 output length
    const sLen = 32; // salt length equals hLen

    if (modLen < hLen + sLen + 2) return false;
    if (em[modLen - 1] !== 0xbc) return false;

    const maskedDB = em.slice(0, modLen - hLen - 1);
    const H = em.slice(modLen - hLen - 1, modLen - 1);

    // emBits = modLen*8-1 → top bit of maskedDB[0] must be 0
    if (maskedDB[0] & 0x80) return false;

    const dbMask = await this.mgf1Sha256(H, modLen - hLen - 1);
    const DB = maskedDB.map((b, i) => b ^ dbMask[i]);
    DB[0] &= 0x7f; // clear top bit per spec

    const zeroPadLen = modLen - hLen - sLen - 2;
    for (let i = 0; i < zeroPadLen; i++) {
      if (DB[i] !== 0) return false;
    }
    if (DB[zeroPadLen] !== 0x01) return false;

    const salt = DB.slice(zeroPadLen + 1);
    const mHash = new Uint8Array(await this.computeDigest(data));
    const mPrime = new Uint8Array(8 + hLen + sLen);
    mPrime.set(mHash, 8);
    mPrime.set(salt, 8 + hLen);

    const hPrime = new Uint8Array(await this.computeDigest(mPrime.buffer));
    return hPrime.every((b, i) => b === H[i]);
  }

  /** Parses an RSA SubjectPublicKeyInfo DER buffer and returns the modulus/exponent as BigInts. */
  private parseRsaSpki(spki: Uint8Array): { n: bigint; e: bigint; modLen: number } {
    const readLen = (offset: number): [number, number] => {
      if (spki[offset] < 0x80) return [spki[offset], 1];
      const numBytes = spki[offset] & 0x7f;
      let len = 0;
      for (let j = 0; j < numBytes; j++) len = (len << 8) | spki[offset + 1 + j];
      return [len, 1 + numBytes];
    };
    let i = 1; // skip outer SEQUENCE tag
    const [, ol] = readLen(i); i += ol;
    i++; // skip AlgorithmIdentifier SEQUENCE tag
    const [algLen, al] = readLen(i); i += al + algLen;
    i++; // skip BIT STRING tag
    const [, bl] = readLen(i); i += bl;
    i++; // skip unused-bits byte
    i++; // skip RSAPublicKey SEQUENCE tag
    const [, rl] = readLen(i); i += rl;

    i++; // n INTEGER tag
    const [nLen, nl] = readLen(i); i += nl;
    const nPad = spki[i] === 0 ? 1 : 0;
    const nBytes = spki.slice(i + nPad, i + nLen);
    i += nLen;

    i++; // e INTEGER tag
    const [eLen, el] = readLen(i); i += el;
    const ePad = spki[i] === 0 ? 1 : 0;
    const eBytes = spki.slice(i + ePad, i + eLen);

    return { n: this.bytesToBigInt(nBytes), e: this.bytesToBigInt(eBytes), modLen: nBytes.length };
  }

  private bytesToBigInt(bytes: Uint8Array): bigint {
    return bytes.reduce((acc, b) => (acc << 8n) | BigInt(b), 0n);
  }

  private bigIntToBytes(n: bigint, len: number): Uint8Array {
    const result = new Uint8Array(len);
    for (let i = len - 1; i >= 0 && n > 0n; i--) {
      result[i] = Number(n & 0xffn);
      n >>= 8n;
    }
    return result;
  }

  private modPow(base: bigint, exp: bigint, mod: bigint): bigint {
    let result = 1n;
    base %= mod;
    while (exp > 0n) {
      if (exp & 1n) result = (result * base) % mod;
      exp >>= 1n;
      base = (base * base) % mod;
    }
    return result;
  }

  private async mgf1Sha256(seed: Uint8Array, len: number): Promise<Uint8Array> {
    const hLen = 32;
    const result = new Uint8Array(len);
    let offset = 0;
    for (let counter = 0; offset < len; counter++) {
      const C = new Uint8Array(4);
      new DataView(C.buffer).setUint32(0, counter);
      const input = new Uint8Array(seed.length + 4);
      input.set(seed);
      input.set(C, seed.length);
      const hash = new Uint8Array(await this.computeDigest(input.buffer));
      const toCopy = Math.min(hLen, len - offset);
      result.set(hash.slice(0, toCopy), offset);
      offset += toCopy;
    }
    return result;
  }

  /**
   * Pure-JS signature verification fallback for non-secure contexts (HTTP).
   * ECDSA P-256 uses @noble/curves; RSA-PSS uses BigInt modular exponentiation.
   * The sig parameter must be the raw file bytes (DER-encoded for ECDSA, raw for RSA-PSS).
   */
  async verifySignature(
    mechanism: string,
    data: ArrayBuffer,
    sig: ArrayBuffer,
    spki: ArrayBuffer,
  ): Promise<boolean> {
    switch (mechanism) {
      case 'ECDSA_P256_SHA256':
        return this.verifyEcdsaP256(data, sig, spki);
      case 'RSA_PSS_SHA256':
        return this.verifyRsaPssSha256(data, sig, spki);
      default:
        throw new Error(`Unsupported mechanism for non-secure context: ${mechanism}`);
    }
  }

  private async verifyEcdsaP256(
    data: ArrayBuffer,
    sig: ArrayBuffer,
    spki: ArrayBuffer,
  ): Promise<boolean> {
    const msgHash = sha256(new Uint8Array(data));
    const pubKey = this.extractEcPoint(new Uint8Array(spki));
    try {
      const der = new Uint8Array(sig);
      // Parse DER SEQUENCE { INTEGER r, INTEGER s }
      let i = 2; // skip 0x30 <len>
      if (der[1] & 0x80) i += der[1] & 0x7f; // multi-byte length
      i++; // skip 0x02
      const rLen = der[i++];
      const rPad = der[i] === 0 ? 1 : 0;
      const r = this.bytesToBigInt(der.slice(i + rPad, i + rLen));
      i += rLen;
      i++; // skip 0x02
      const sLen = der[i++];
      const sPad = der[i] === 0 ? 1 : 0;
      const s = this.bytesToBigInt(der.slice(i + sPad, i + sLen));
      // P1363: r and s each padded to 32 bytes
      const p1363 = new Uint8Array(64);
      p1363.set(this.bigIntToBytes(r, 32), 0);
      p1363.set(this.bigIntToBytes(s, 32), 32);
      return p256.verify(p1363, msgHash, pubKey);
    } catch {
      return false;
    }
  }

  /** Extracts the raw EC point bytes from a SubjectPublicKeyInfo (SPKI) DER buffer. */
  private extractEcPoint(spki: Uint8Array): Uint8Array {
    const readLen = (offset: number): [number, number] => {
      if (spki[offset] < 0x80) return [spki[offset], 1];
      const n = spki[offset] & 0x7f;
      let len = 0;
      for (let j = 0; j < n; j++) len = (len << 8) | spki[offset + 1 + j];
      return [len, 1 + n];
    };
    let i = 1; // skip outer SEQUENCE tag
    const [, outerLB] = readLen(i); i += outerLB;
    i++; // skip AlgorithmIdentifier SEQUENCE tag
    const [algLen, algLB] = readLen(i); i += algLB + algLen;
    i++; // skip BIT STRING tag
    const [, bsLB] = readLen(i); i += bsLB;
    i++; // skip unused-bits byte (0x00)
    return spki.slice(i);
  }

  /**
   * RSA-PSS-SHA256 verification per RFC 8017 §9.1.2 using BigInt.
   * Assumes saltLength === 32 (= hLen for SHA-256), which matches the backend default.
   */
  private async verifyRsaPssSha256(
    data: ArrayBuffer,
    sig: ArrayBuffer,
    spki: ArrayBuffer,
  ): Promise<boolean> {
    const { n, e, modLen } = this.parseRsaSpki(new Uint8Array(spki));

    const s = this.bytesToBigInt(new Uint8Array(sig));
    const m = this.modPow(s, e, n);
    const em = this.bigIntToBytes(m, modLen);

    const hLen = 32; // SHA-256 output length
    const sLen = 32; // salt length equals hLen

    if (modLen < hLen + sLen + 2) return false;
    if (em[modLen - 1] !== 0xbc) return false;

    const maskedDB = em.slice(0, modLen - hLen - 1);
    const H = em.slice(modLen - hLen - 1, modLen - 1);

    // emBits = modLen*8-1 → top bit of maskedDB[0] must be 0
    if (maskedDB[0] & 0x80) return false;

    const dbMask = await this.mgf1Sha256(H, modLen - hLen - 1);
    const DB = maskedDB.map((b, i) => b ^ dbMask[i]);
    DB[0] &= 0x7f; // clear top bit per spec

    const zeroPadLen = modLen - hLen - sLen - 2;
    for (let i = 0; i < zeroPadLen; i++) {
      if (DB[i] !== 0) return false;
    }
    if (DB[zeroPadLen] !== 0x01) return false;

    const salt = DB.slice(zeroPadLen + 1);
    const mHash = new Uint8Array(await this.computeDigest(data));
    const mPrime = new Uint8Array(8 + hLen + sLen);
    mPrime.set(mHash, 8);
    mPrime.set(salt, 8 + hLen);

    const hPrime = new Uint8Array(await this.computeDigest(mPrime.buffer));
    return hPrime.every((b, i) => b === H[i]);
  }

  /** Parses an RSA SubjectPublicKeyInfo DER buffer and returns the modulus/exponent as BigInts. */
  private parseRsaSpki(spki: Uint8Array): { n: bigint; e: bigint; modLen: number } {
    const readLen = (offset: number): [number, number] => {
      if (spki[offset] < 0x80) return [spki[offset], 1];
      const numBytes = spki[offset] & 0x7f;
      let len = 0;
      for (let j = 0; j < numBytes; j++) len = (len << 8) | spki[offset + 1 + j];
      return [len, 1 + numBytes];
    };
    let i = 1; // skip outer SEQUENCE tag
    const [, ol] = readLen(i); i += ol;
    i++; // skip AlgorithmIdentifier SEQUENCE tag
    const [algLen, al] = readLen(i); i += al + algLen;
    i++; // skip BIT STRING tag
    const [, bl] = readLen(i); i += bl;
    i++; // skip unused-bits byte
    i++; // skip RSAPublicKey SEQUENCE tag
    const [, rl] = readLen(i); i += rl;

    i++; // n INTEGER tag
    const [nLen, nl] = readLen(i); i += nl;
    const nPad = spki[i] === 0 ? 1 : 0;
    const nBytes = spki.slice(i + nPad, i + nLen);
    i += nLen;

    i++; // e INTEGER tag
    const [eLen, el] = readLen(i); i += el;
    const ePad = spki[i] === 0 ? 1 : 0;
    const eBytes = spki.slice(i + ePad, i + eLen);

    return { n: this.bytesToBigInt(nBytes), e: this.bytesToBigInt(eBytes), modLen: nBytes.length };
  }

  private bytesToBigInt(bytes: Uint8Array): bigint {
    return bytes.reduce((acc, b) => (acc << 8n) | BigInt(b), 0n);
  }

  private bigIntToBytes(n: bigint, len: number): Uint8Array {
    const result = new Uint8Array(len);
    for (let i = len - 1; i >= 0 && n > 0n; i--) {
      result[i] = Number(n & 0xffn);
      n >>= 8n;
    }
    return result;
  }

  private modPow(base: bigint, exp: bigint, mod: bigint): bigint {
    let result = 1n;
    base %= mod;
    while (exp > 0n) {
      if (exp & 1n) result = (result * base) % mod;
      exp >>= 1n;
      base = (base * base) % mod;
    }
    return result;
  }

  private async mgf1Sha256(seed: Uint8Array, len: number): Promise<Uint8Array> {
    const hLen = 32;
    const result = new Uint8Array(len);
    let offset = 0;
    for (let counter = 0; offset < len; counter++) {
      const C = new Uint8Array(4);
      new DataView(C.buffer).setUint32(0, counter);
      const input = new Uint8Array(seed.length + 4);
      input.set(seed);
      input.set(C, seed.length);
      const hash = new Uint8Array(await this.computeDigest(input.buffer));
      const toCopy = Math.min(hLen, len - offset);
      result.set(hash.slice(0, toCopy), offset);
      offset += toCopy;
    }
    return result;
  }
}
