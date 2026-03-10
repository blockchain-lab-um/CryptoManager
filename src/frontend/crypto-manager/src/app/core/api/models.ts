export interface CreateKeyRequestDto {
    name: string;
    purpose: string;
    allowedMechanisms: string[];
}

export interface CreateKeyResponseDto {
    keyId?: string | null;
    name?: string | null;
    purpose?: string | null;
    primaryVersion: number;
    publicKeyPem?: string | null;
}

export interface RotateKeyResponseDto {
    keyId?: string | null;
    newPrimaryVersion: number;
    publicKeyPem?: string | null;
}

export interface GetPublicKeyResponseDto {
    keyId?: string | null;
    keyVersion: number;
    publicKeyPem?: string | null;
}

export interface SignDigestRequestDto {
    keyId: string;
    mechanism: string;
    digestBase64: string;
}

export interface SignFileRequestDto {
    keyId: string;
    mechanism: string;
    file: File;
}

export interface SignFileResponseDto {
    keyId?: string | null;
    keyVersion: number;
    mechanism?: string | null;
    encoding?: string | null;
    signedFile: File;
    signedFormat?: string | null;
    auditEventId?: string | null;
}


export interface SignDigestResponseDto {
    keyId?: string | null;
    keyVersion: number;
    mechanism?: string | null;
    encoding?: string | null;
    signatureBase64?: string | null;
    auditEventId?: string | null;
}

export interface KeySummaryDto {
    keyId?: string | null;
    name?: string | null;
    purpose?: string | null;
    state?: string | null;
    versionCount: number;
    primaryVersion?: number | null;
    createdAt: string; // ISO date-time
}

export interface ListKeysResponseDto {
    keys?: KeySummaryDto[] | null;
}
