import { Component, inject, input, output, signal } from '@angular/core';
import { ReactiveFormsModule } from '@angular/forms';
import { AutoCompleteModule } from 'primeng/autocomplete';
import { ButtonModule } from 'primeng/button';
import { DialogModule } from 'primeng/dialog';
import { InputTextModule } from 'primeng/inputtext';
import { MessageModule } from 'primeng/message';
import { SelectModule } from 'primeng/select';
import { KeySummaryDto } from '../../../../core/api/models';
import { KeysService } from '../../keys.service';
import { ProgressSpinner } from "primeng/progressspinner";
import { AppMessenger } from '../../../../core/services/app-messenger';

@Component({
  selector: 'app-key-delete-dialog',
  imports: [
    ReactiveFormsModule,
    DialogModule,
    ButtonModule,
    InputTextModule,
    AutoCompleteModule,
    SelectModule,
    MessageModule,
    ProgressSpinner
  ],
  templateUrl: './key-delete-dialog.html',
})
export class KeyDeleteDialog {
  private keysService = inject(KeysService);
  private appMessenger = inject(AppMessenger);

  visible = input.required<boolean>();
  visibleChange = output<boolean>();
  deleted = output<void>();

  loading = signal(false);
  error = signal<string | null>(null);

  key = input<KeySummaryDto | null>(null);

  deleteKey() {
    if (!this.key()?.keyId) return;

    this.loading.set(true);
    this.keysService.deleteKey(this.key()!.keyId!).subscribe({
      next: () => {
        this.deleted.emit();
        this.error.set(null);
        this.visibleChange.emit(false);
        this.appMessenger.showMessage('success', 'Key deleted', `Key ${this.key()!.keyId} has been deleted successfully.`);
      },
      error: (err) => {
        console.error(err);
        this.error.set('Failed to delete key: ' + (err?.error?.Error || err.message || 'Unknown error'));
        this.loading.set(false);
      },
      complete: () => {
        this.loading.set(false);
      }
    });
  }
}
