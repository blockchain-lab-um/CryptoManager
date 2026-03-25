import { CertificateSource, CertificateStatus } from '../../core/api/models';

export function statusSeverity(status: CertificateStatus): 'success' | 'info' | 'warn' | 'danger' {
  switch (status) {
    case 'Active':
      return 'success';
    case 'PendingEnrollment':
    case 'Superseded':
      return 'info';
    case 'Expired':
      return 'warn';
    case 'ImportFailed':
    case 'Revoked':
      return 'danger';
    default:
      return 'info';
  }
}

export function sourceLabel(source: CertificateSource): string {
  switch (source) {
    case 'SelfSigned':
      return 'Self-signed';
    case 'ExternalCA':
      return 'External CA';
    case 'Imported':
      return 'Imported';
    default:
      return source;
  }
}

export function certStatusBadgeClass(status: CertificateStatus): string {
  switch (status) {
    case 'Active':
      return 'bg-emerald-100 text-emerald-800';
    case 'PendingEnrollment':
      return 'bg-sky-100 text-sky-800';
    case 'Superseded':
      return 'bg-slate-100 text-slate-800';
    case 'Expired':
      return 'bg-amber-100 text-amber-800';
    case 'ImportFailed':
    case 'Revoked':
      return 'bg-rose-100 text-rose-800';
    default:
      return 'bg-zinc-100 text-zinc-800';
  }
}

export function isExpiringSoon(notAfter: string, now: Date): boolean {
  const expiry = new Date(notAfter);
  const daysUntilExpiry = (expiry.getTime() - now.getTime()) / (1000 * 60 * 60 * 24);
  return daysUntilExpiry <= 30;
}

export function isExpired(notAfter: string, now: Date): boolean {
  return new Date(notAfter).getTime() < now.getTime();
}
