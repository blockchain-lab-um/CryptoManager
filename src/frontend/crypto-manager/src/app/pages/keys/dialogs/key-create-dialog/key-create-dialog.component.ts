import { ChangeDetectionStrategy, Component, inject, input, output, signal } from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';

import { AutoCompleteCompleteEvent, AutoCompleteModule } from 'primeng/autocomplete';
import { ButtonModule } from 'primeng/button';
import { DialogModule } from 'primeng/dialog';
import { InputTextModule } from 'primeng/inputtext';
import { MessageModule } from 'primeng/message';
import { SelectModule } from 'primeng/select';

import { CryptoManagerApi } from '../../../../core/api/cryptomanager-api.service';

@Component({
  selector: 'app-key-create-dialog',
  imports: [
    ReactiveFormsModule,
    DialogModule,
    ButtonModule,
    InputTextModule,
    AutoCompleteModule,
    SelectModule,
    MessageModule,
  ],
  templateUrl: './key-create-dialog.component.html',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class KeyCreateDialogComponent {
  private api = inject(CryptoManagerApi);
  private fb = inject(FormBuilder);

  visible = input.required<boolean>();
  visibleChange = output<boolean>();
  created = output<void>();

  error = signal<string | null>(null);

  form = this.fb.group({
    name: ['', [Validators.required, Validators.minLength(1)]],
    purpose: ['', [Validators.required, Validators.minLength(1)]],
    allowedMechanisms: this.fb.control<string[]>([], [Validators.required]),
  });

  readonly keyPurposes = [{ label: 'Sign', value: 'Sign' }];

  mechanismOptions = ['RSA_PSS_SHA256', 'CKM_RSA_PKCS', 'CKM_RSA_PKCS_PSS', 'CKM_ECDSA'];
  filteredMechanisms = this.mechanismOptions;

  searchMechanisms(event: AutoCompleteCompleteEvent) {
    this.filteredMechanisms = this.mechanismOptions.filter(m =>
      m.toLowerCase().includes((event.query ?? '').toLowerCase())
    );
  }

  close() {
    this.error.set(null);
    this.form.reset();
    this.visibleChange.emit(false);
  }

  submit() {
    if (this.form.invalid) return;

    const { name, purpose, allowedMechanisms } = this.form.getRawValue();

    this.api.createKey({
      name: name!,
      purpose: purpose!,
      allowedMechanisms: allowedMechanisms ?? [],
    }).subscribe({
      next: () => {
        this.close();
        this.created.emit();
      },
      error: (e) => this.error.set(e?.error?.message ?? 'Failed to create key'),
    });
  }
}
