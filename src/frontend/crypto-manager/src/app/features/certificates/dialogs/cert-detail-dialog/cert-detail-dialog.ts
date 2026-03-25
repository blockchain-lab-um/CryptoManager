import { DatePipe } from '@angular/common';
import { ChangeDetectionStrategy, Component, input, output } from '@angular/core';

import { ButtonModule } from 'primeng/button';
import { DialogModule } from 'primeng/dialog';
import { TagModule } from 'primeng/tag';

import { ActiveCertificateDto } from '../../../../core/api/models';
import { certStatusBadgeClass, sourceLabel, statusSeverity } from '../../certificates.utils';

@Component({
  selector: 'app-cert-detail-dialog',
  imports: [
    DatePipe,
    DialogModule,
    ButtonModule,
    TagModule,
  ],
  templateUrl: './cert-detail-dialog.html',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class CertDetailDialog {
  readonly statusSeverity = statusSeverity;
  readonly sourceLabel = sourceLabel;
  readonly certStatusBadgeClass = certStatusBadgeClass;

  visible = input.required<boolean>();
  cert = input<ActiveCertificateDto | null>(null);
  visibleChange = output<boolean>();

  copy(text?: string | null): void {
    if (text) navigator.clipboard.writeText(text);
  }
}
