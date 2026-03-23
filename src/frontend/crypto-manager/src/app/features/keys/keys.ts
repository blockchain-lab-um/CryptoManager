import { ChangeDetectionStrategy, Component, inject, signal } from '@angular/core';
import { DatePipe } from '@angular/common';
import { RouterLink } from '@angular/router';

import { TableModule } from 'primeng/table';
import { ButtonModule } from 'primeng/button';
import { InputTextModule } from 'primeng/inputtext';
import { TagModule } from 'primeng/tag';

import { KeysService } from './keys.service';
import { stateSeverity } from './keys.utils';
import { KeySummaryDto } from '../../core/api/models';
import { Card } from '../../shared/components/card/card';
import { KeyCreateDialog } from './dialogs/key-create-dialog/key-create-dialog';
import { KeyPublicDialog } from './dialogs/key-public-dialog/key-public-dialog';
import { KeyRotateDialog } from './dialogs/key-rotate-dialog/key-rotate-dialog';
import { KeyDeleteDialog } from './dialogs/key-delete-dialog/key-delete-dialog';

@Component({
  imports: [
    DatePipe,
    RouterLink,
    TableModule,
    ButtonModule,
    InputTextModule,
    TagModule,
    Card,
    KeyCreateDialog,
    KeyPublicDialog,
    KeyRotateDialog,
    KeyDeleteDialog,
  ],
  templateUrl: './keys.html',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class KeysPage {
  protected keysService = inject(KeysService);
  protected readonly stateSeverity = stateSeverity;

  showCreate = signal(false);
  showPublic = signal(false);
  showRotate = signal(false);
  showDelete = signal(false);
  selectedKey = signal<KeySummaryDto | null>(null);

  openCreate() {
    this.showCreate.set(true);
  }

  openPublic(key: KeySummaryDto) {
    this.selectedKey.set(key);
    this.showPublic.set(true);
  }

  openRotate(key: KeySummaryDto) {
    this.selectedKey.set(key);
    this.showRotate.set(true);
  }

  openDelete(key: KeySummaryDto) {
    this.selectedKey.set(key);
    this.showDelete.set(true);
  }

  copy(text: string | null | undefined) {
    if (text) navigator.clipboard.writeText(text);
  }
}
