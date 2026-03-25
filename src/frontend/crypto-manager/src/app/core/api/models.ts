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

export type CertificateStatus =
    | 'PendingEnrollment'
    | 'Active'
    | 'Expired'
    | 'Revoked'
    | 'Superseded'
    | 'ImportFailed';

export type CertificateSource = 'SelfSigned' | 'ExternalCA' | 'Imported';

export interface ActiveCertificateDto {
    id: string;
    keyVersionId: string;
    status: CertificateStatus;
    source: CertificateSource;
    serialNumber?: string | null;
    thumbprint?: string | null;
    subjectDN?: string | null;
    issuerDN?: string | null;
    notBefore?: string | null;
    notAfter?: string | null;
    enrollmentId?: string | null;
    createdAt: string;
    createdBy: string;
}

export interface EnrollCertificateRequestDto {
    commonName: string;
    organization?: string | null;
    organizationalUnit?: string | null;
    country?: string | null;
}

export interface EnrollCertificateResponseDto {
    certificateId: string;
    enrollmentId: string;
}

export interface CompleteEnrollmentResponseDto {
    isComplete: boolean;
    certificateId?: string | null;
    thumbprint?: string | null;
}

export interface ImportCertificateRequestDto {
    certificateDerBase64: string;
    chainDerBase64?: string[] | null;
}

export interface ImportCertificateResponseDto {
    certificateId: string;
    thumbprint: string;
}

export interface RevokeCertificateRequestDto {
    reason?: string | null;
}
