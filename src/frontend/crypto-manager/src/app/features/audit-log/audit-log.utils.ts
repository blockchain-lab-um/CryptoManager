// Severity used for the outcome badge — derived from entry.success, not a backend field.
export function outcomeSeverity(success: boolean): 'success' | 'danger' {
  return success ? 'success' : 'danger';
}

export function actionLabel(action: string): string {
  switch (action) {
    case 'CreateKey': return 'Create Key';
    case 'RotateKey': return 'Rotate Key';
    case 'DisableKey': return 'Disable Key';
    case 'DeleteKey': return 'Delete Key';
    case 'Sign': return 'Sign';
    case 'GetPublicKey': return 'Get Public Key';
    case 'SignFile': return 'Sign File';
    default: return action;
  }
}

export function actionBadgeClass(action: string): string {
  switch (action) {
    case 'CreateKey':
      return 'bg-emerald-100 text-emerald-800';
    case 'RotateKey':
      return 'bg-sky-100 text-sky-800';
    case 'DisableKey':
      return 'bg-amber-100 text-amber-800';
    case 'DeleteKey':
      return 'bg-rose-100 text-rose-800';
    case 'Sign':
      return 'bg-violet-100 text-violet-800';
    case 'GetPublicKey':
      return 'bg-slate-100 text-slate-800';
    case 'SignFile':
      return 'bg-cyan-100 text-cyan-800';
    default:
      return 'bg-zinc-100 text-zinc-800';
  }
}

// Values match the backend AuditAction enum names exactly.
export const AUDIT_ACTION_OPTIONS: { label: string; value: string | null }[] = [
  { label: 'All actions', value: null },
  { label: 'Create Key', value: 'CreateKey' },
  { label: 'Rotate Key', value: 'RotateKey' },
  { label: 'Disable Key', value: 'DisableKey' },
  { label: 'Delete Key', value: 'DeleteKey' },
  { label: 'Sign', value: 'Sign' },
  { label: 'Get Public Key', value: 'GetPublicKey' },
  { label: 'Sign File', value: 'SignFile' },
];
