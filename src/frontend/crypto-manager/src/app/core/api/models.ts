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
    owner?: string | null;
}

export interface ListKeysResponseDto {
    keys?: KeySummaryDto[] | null;
}

export interface AuditLogEntryDto {
    id: string;
    timestamp: string; // ISO 8601
    actor: string;
    actorId?: string | null;
    action: string;
    keyName?: string | null;
    keyId?: string | null;
    keyVersion?: number | null;
    mechanism?: string | null;
    success: boolean;
    error?: string | null;
}

export interface PagedAuditLogResultDto {
    items: AuditLogEntryDto[];
    page: number;
    pageSize: number;
    totalCount: number;
    totalPages: number;
}

export interface AuditLogQueryParams {
    from?: string;
    to?: string;
    action?: string;
    keyId?: string;
    actor?: string;
    page: number;
    pageSize: number;
}
