import { ChangeDetectionStrategy, Component, inject, signal } from '@angular/core';
import { DatePipe } from '@angular/common';
import { RouterLink } from '@angular/router';

import { TableModule } from 'primeng/table';
import { ButtonModule } from 'primeng/button';
import { InputTextModule } from 'primeng/inputtext';
import { TagModule } from 'primeng/tag';

import { KeysService } from '../keys.service';
import { KeySummaryDto } from '../../../core/api/models';
import { Card } from '../../../core/components/card/card';
import { KeyCreateDialogComponent } from '../dialogs/key-create-dialog/key-create-dialog.component';
import { KeyPublicDialogComponent } from '../dialogs/key-public-dialog/key-public-dialog.component';
import { KeyRotateDialogComponent } from '../dialogs/key-rotate-dialog/key-rotate-dialog.component';

@Component({
  imports: [
    DatePipe,
    RouterLink,
    TableModule,
    ButtonModule,
    InputTextModule,
    TagModule,
    Card,
    KeyCreateDialogComponent,
    KeyPublicDialogComponent,
    KeyRotateDialogComponent,
  ],
  templateUrl: './keys.component.html',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class KeysComponent {
  protected keysService = inject(KeysService);

  showCreate = signal(false);
  showPublic = signal(false);
  showRotate = signal(false);
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

  copy(text: string | null | undefined) {
    if (text) navigator.clipboard.writeText(text);
  }
}
