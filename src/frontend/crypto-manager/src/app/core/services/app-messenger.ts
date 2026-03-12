import { inject, Injectable } from '@angular/core';
import { MessageService } from 'primeng/api';

@Injectable({
  providedIn: 'root',
})
export class AppMessenger {
  private messageService = inject(MessageService);

  showMessage(severity: 'success' | 'info' | 'warn' | 'error', summary: string, detail?: string) {
    this.messageService.add({ severity, summary, detail, life: 3000 });
  }
}
