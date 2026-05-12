import { ChangeDetectionStrategy, Component, inject, OnInit, signal } from '@angular/core';
import { DatePipe } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { ButtonModule } from 'primeng/button';
import { InputTextModule } from 'primeng/inputtext';
import { TagModule } from 'primeng/tag';
import { ConfirmDialogModule } from 'primeng/confirmdialog';
import { ConfirmationService, MessageService } from 'primeng/api';
import { ToastModule } from 'primeng/toast';
import { WebAuthnService, CredentialView } from '../webauthn/webauthn.service';

@Component({
  selector: 'app-passkeys',
  imports: [DatePipe, FormsModule, ButtonModule, InputTextModule, TagModule, ConfirmDialogModule, ToastModule],
  providers: [ConfirmationService, MessageService],
  templateUrl: './passkeys.html',
  styleUrl: './passkeys.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class PasskeysComponent implements OnInit {
  private readonly webauthn = inject(WebAuthnService);
  private readonly confirmationService = inject(ConfirmationService);
  private readonly messageService = inject(MessageService);

  credentials = signal<CredentialView[]>([]);
  loading = signal(false);
  editingId = signal<string | null>(null);
  editingNickname = signal('');

  ngOnInit() {
    this.loadCredentials();
  }

  private loadCredentials() {
    this.webauthn.list().subscribe({
      next: creds => this.credentials.set(creds),
      error: () => this.messageService.add({ severity: 'error', summary: 'Failed to load passkeys.' }),
    });
  }

  badgeLabel(cred: CredentialView): string {
    return cred.isBackedUp || cred.transports.includes('internal') ? 'synced' : 'security key';
  }

  badgeSeverity(cred: CredentialView): 'success' | 'info' {
    return cred.isBackedUp || cred.transports.includes('internal') ? 'success' : 'info';
  }

  startEdit(cred: CredentialView) {
    this.editingId.set(cred.id);
    this.editingNickname.set(cred.nickname);
  }

  saveNickname(cred: CredentialView) {
    const name = this.editingNickname().trim();
    if (!name || name === cred.nickname) { this.editingId.set(null); return; }
    this.webauthn.rename(cred.id, name).subscribe({
      next: () => { this.editingId.set(null); this.loadCredentials(); },
      error: () => this.messageService.add({ severity: 'error', summary: 'Rename failed.' }),
    });
  }

  cancelEdit() {
    this.editingId.set(null);
  }

  async addPasskey(type: 'platform' | 'cross-platform') {
    this.loading.set(true);
    try {
      const label = type === 'platform' ? 'Passkey' : 'Security Key';
      const nickname = `My ${label}`;
      await this.webauthn.registerPasskey(type, nickname);
      this.messageService.add({ severity: 'success', summary: `${label} added.` });
      this.loadCredentials();
    } catch (e: unknown) {
      const msg = e instanceof Error ? e.message : 'Failed to add passkey.';
      this.messageService.add({ severity: 'error', summary: msg });
    } finally {
      this.loading.set(false);
    }
  }

  confirmDelete(cred: CredentialView) {
    const isLast = this.credentials().length === 1;

    if (isLast) {
      this.confirmationService.confirm({
        header: 'Remove last passkey?',
        message: 'You will still be able to sign in with your password.',
        acceptLabel: 'Remove',
        rejectLabel: 'Cancel',
        accept: () => this.doDelete(cred.id),
      });
    } else {
      this.confirmationService.confirm({
        header: 'Remove passkey?',
        message: `Remove "${cred.nickname}"?`,
        acceptLabel: 'Remove',
        rejectLabel: 'Cancel',
        accept: () => this.doDelete(cred.id),
      });
    }
  }

  private doDelete(id: string) {
    this.webauthn.delete(id).subscribe({
      next: () => { this.messageService.add({ severity: 'success', summary: 'Passkey removed.' }); this.loadCredentials(); },
      error: () => this.messageService.add({ severity: 'error', summary: 'Delete failed.' }),
    });
  }
}
