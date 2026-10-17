import { ChangeDetectionStrategy, Component, computed, inject, signal } from '@angular/core';
import { DatePipe } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { RouterLink } from '@angular/router';

import { TableModule } from 'primeng/table';
import { ButtonModule } from 'primeng/button';
import { InputTextModule } from 'primeng/inputtext';
import { SelectButtonModule } from 'primeng/selectbutton';
import { TagModule } from 'primeng/tag';

import { KeysService } from './keys.service';
import { stateSeverity } from './keys.utils';
import { KeySummaryDto } from '../../core/api/models';
import { Card } from '../../shared/components/card/card';
import { KeyCreateDialog } from './dialogs/key-create-dialog/key-create-dialog';
import { KeyPublicDialog } from './dialogs/key-public-dialog/key-public-dialog';
import { KeyRotateDialog } from './dialogs/key-rotate-dialog/key-rotate-dialog';
import { KeyDeleteDialog } from './dialogs/key-delete-dialog/key-delete-dialog';
import { AuthService } from '../../auth/auth.service';

@Component({
  imports: [
    DatePipe,
    FormsModule,
    RouterLink,
    TableModule,
    ButtonModule,
    InputTextModule,
    SelectButtonModule,
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
  protected readonly authService = inject(AuthService);
  protected readonly stateSeverity = stateSeverity;

  showCreate = signal(false);
  showPublic = signal(false);
  showRotate = signal(false);
  showDelete = signal(false);
  selectedKey = signal<KeySummaryDto | null>(null);

  isAdmin = this.authService.isAdmin;

  showOnlyMine = signal(false);

  keyScope = computed(() => this.showOnlyMine() ? 'mine' : 'all');

  scopeOptions = [
    { label: 'All keys', value: 'all' },
    { label: 'My keys', value: 'mine' },
  ];

  filteredKeys = computed(() => {
    const keys = this.keysService.keysResource.value() ?? [];
    if (!this.isAdmin() || !this.showOnlyMine()) return keys;
    const userName = this.authService.currentUser()?.userName;
    return keys.filter(k => k.owner === userName);
  });

  onScopeChange(value: string) {
    this.showOnlyMine.set(value === 'mine');
  }

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
