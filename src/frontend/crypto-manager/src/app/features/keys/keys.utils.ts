import { KeySummaryDto } from '../../core/api/models';

export function stateSeverity(state?: string | null): 'success' | 'info' | 'warn' | 'danger' {
  const s = (state ?? '').toLowerCase();
  if (s.includes('active') || s.includes('ready')) return 'success';
  if (s.includes('pending')) return 'info';
  if (s.includes('disabled') || s.includes('inactive')) return 'warn';
  if (s.includes('revoked') || s.includes('deleted') || s.includes('error')) return 'danger';
  return 'info';
}

export function toKeySelectOptions(keys: KeySummaryDto[]): { label: string; value: string }[] {
  return keys.map(k => ({
    label: k.name || k.keyId || 'Unnamed key',
    value: k.keyId ?? '',
  }));
}
