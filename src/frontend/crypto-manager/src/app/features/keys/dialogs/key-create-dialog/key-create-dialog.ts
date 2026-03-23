import { ChangeDetectionStrategy, Component, inject, input, output, signal } from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';

import { AutoCompleteCompleteEvent, AutoCompleteModule } from 'primeng/autocomplete';
import { ButtonModule } from 'primeng/button';
import { DialogModule } from 'primeng/dialog';
import { InputTextModule } from 'primeng/inputtext';
import { MessageModule } from 'primeng/message';
import { ProgressSpinner } from 'primeng/progressspinner';
import { SelectModule } from 'primeng/select';

import { AppMessenger } from '../../../../core/services/app-messenger';
import { KeysService } from '../../keys.service';

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
    ProgressSpinner,
  ],
  templateUrl: './key-create-dialog.html',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class KeyCreateDialog {
  private keysService = inject(KeysService);
  private appMessenger = inject(AppMessenger);
  private fb = inject(FormBuilder);

  visible = input.required<boolean>();
  visibleChange = output<boolean>();
  created = output<void>();

  loading = signal(false);
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

    this.loading.set(true);
    this.keysService.createKey({
      name: name!,
      purpose: purpose!,
      allowedMechanisms: allowedMechanisms ?? [],
    }).subscribe({
      next: () => {
        this.created.emit();
        this.error.set(null);
        this.close();
        this.appMessenger.showMessage('success', 'Key created', `Key "${name}" has been created successfully.`);
      },
      error: (err) => {
        this.error.set('Failed to create key: ' + (err?.error?.Error || err.message || 'Unknown error'));
        this.loading.set(false);
      },
      complete: () => {
        this.loading.set(false);
      },
    });
  }
}