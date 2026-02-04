import { ChangeDetectionStrategy, Component, inject, signal } from '@angular/core';
import { DatePipe } from '@angular/common';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { RouterLink } from '@angular/router';
import { rxResource } from '@angular/core/rxjs-interop';
import { map } from 'rxjs';

import { TableModule } from 'primeng/table';
import { ButtonModule } from 'primeng/button';
import { InputTextModule } from 'primeng/inputtext';
import { TagModule } from 'primeng/tag';
import { DialogModule } from 'primeng/dialog';
import { InputNumberModule } from 'primeng/inputnumber';
import { TextareaModule } from 'primeng/textarea';
import { MessageModule } from 'primeng/message';
import { ProgressSpinnerModule } from 'primeng/progressspinner';

import { CryptoManagerApi } from '../../../core/api/cryptomanager-api.service';
import { KeySummaryDto } from '../../../core/api/models';

@Component({
  imports: [
    DatePipe,
    ReactiveFormsModule,
    TableModule,
    ButtonModule,
    InputTextModule,
    TagModule,
    DialogModule,
    InputNumberModule,
    TextareaModule,
    MessageModule,
    RouterLink,
    ProgressSpinnerModule
  ],
  templateUrl: './keys.component.html',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class KeysComponent {
  private api = inject(CryptoManagerApi);
  private fb = inject(FormBuilder);

  keysResource = rxResource({
    stream: () => this.api.listKeys().pipe(map(res => res.keys ?? [])),
  });

  error = signal<string | null>(null);

  // dialogs
  showCreate = signal(false);
  showPublic = signal(false);
  showRotate = signal(false);

  selectedKey = signal<KeySummaryDto | null>(null);

  createForm = this.fb.group({
    name: ['', [Validators.required, Validators.minLength(1)]],
    purpose: ['', [Validators.required, Validators.minLength(1)]],
    allowedMechanismsCsv: ['CKM_RSA_PKCS', [Validators.required, Validators.minLength(1)]],
  });

  publicForm = this.fb.group({
    version: this.fb.control<number | null>(null),
  });
  publicPem = signal<string | null>(null);

  rotateResult = signal<any>(null);
  rotating = signal(false);

  refresh() {
    this.keysResource.reload();
  }

  stateSeverity(state?: string | null): 'success' | 'info' | 'warn' | 'danger' {
    const s = (state ?? '').toLowerCase();
    if (s.includes('active') || s.includes('ready')) return 'success';
    if (s.includes('pending')) return 'info';
    if (s.includes('disabled') || s.includes('inactive')) return 'warn';
    if (s.includes('revoked') || s.includes('deleted') || s.includes('error')) return 'danger';
    return 'info';
  }

  openCreate() {
    this.createForm.reset({
      name: '',
      purpose: '',
      allowedMechanismsCsv: 'CKM_RSA_PKCS'
    });
    this.showCreate.set(true);
  }

  submitCreate() {
    if (this.createForm.invalid) return;

    const { name, purpose, allowedMechanismsCsv } = this.createForm.getRawValue();
    const allowedMechanisms = String(allowedMechanismsCsv)
      .split(',')
      .map(x => x.trim())
      .filter(Boolean);

    this.api.createKey({ name: name!, purpose: purpose!, allowedMechanisms }).subscribe({
      next: () => {
        this.showCreate.set(false);
        this.refresh();
      },
      error: (e) => this.error.set(e?.error?.message ?? 'Failed to create key')
    });
  }

  openPublic(key: KeySummaryDto) {
    this.selectedKey.set(key);
    this.publicForm.reset({ version: null });
    this.publicPem.set(null);
    this.showPublic.set(true);
  }

  fetchPublic() {
    const key = this.selectedKey();
    if (!key?.keyId) return;
    const { version } = this.publicForm.getRawValue();

    this.publicPem.set(null);
    this.api.getPublicKey(key.keyId, version).subscribe({
      next: (res) => this.publicPem.set(res.publicKeyPem ?? ''),
      error: (e) => this.error.set(e?.error?.message ?? 'Failed to fetch public key')
    });
  }

  openRotate(key: KeySummaryDto) {
    this.selectedKey.set(key);
    this.rotateResult.set(null);
    this.showRotate.set(true);
  }

  doRotate() {
    const key = this.selectedKey();
    if (!key?.keyId) return;

    this.rotating.set(true);
    this.api.rotateKey(key.keyId).subscribe({
      next: (res) => {
        this.rotateResult.set(res);
        this.rotating.set(false);
      },
      error: (e) => {
        this.error.set(e?.error?.message ?? 'Failed to rotate key');
        this.rotating.set(false);
      },
    });
  }

  copy(text: string | null | undefined) {
    if (!text) return;
    navigator.clipboard.writeText(text);
  }
}
