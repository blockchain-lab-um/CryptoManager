import {
  ChangeDetectionStrategy,
  Component,
  ElementRef,
  computed,
  input,
  output,
  signal,
  viewChild,
} from '@angular/core';
import { DecimalPipe } from '@angular/common';
import * as pdfjsLib from 'pdfjs-dist';
import { ButtonModule } from 'primeng/button';
import { DialogModule } from 'primeng/dialog';
import { StampPosition } from '../../core/api/models';

pdfjsLib.GlobalWorkerOptions.workerSrc = '/pdf.worker.min.mjs';

const HANDLE_IDS = ['n', 's', 'e', 'w', 'nw', 'ne', 'sw', 'se'] as const;
type HandleId = (typeof HANDLE_IDS)[number];

@Component({
  selector: 'app-stamp-position-dialog',
  imports: [DialogModule, ButtonModule, DecimalPipe],
  templateUrl: './stamp-position-dialog.html',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class StampPositionDialog {
  visible = input.required<boolean>();
  pdfFile = input.required<File>();
  initialPosition = input<StampPosition | null>(null);
  visibleChange = output<boolean>();
  applied = output<StampPosition | null>();

  protected readonly canvasRef = viewChild.required<ElementRef<HTMLCanvasElement>>('canvas');
  protected readonly previewRef = viewChild.required<ElementRef<HTMLDivElement>>('preview');

  protected readonly handles = HANDLE_IDS;

  protected pageW = signal(0);
  protected pageH = signal(0);

  protected boxX = signal(0);
  protected boxY = signal(0);
  protected boxW = signal(0);
  protected boxH = signal(0);
  protected boxAngle = signal(0);

  protected normX = computed(() => (this.pageW() > 0 ? this.boxX() / this.pageW() : 0));
  protected normY = computed(() => (this.pageH() > 0 ? this.boxY() / this.pageH() : 0));
  protected normW = computed(() => (this.pageW() > 0 ? this.boxW() / this.pageW() : 0));
  protected normH = computed(() => (this.pageH() > 0 ? this.boxH() / this.pageH() : 0));

  private scale = 1;

  async renderPage() {
    const canvas = this.canvasRef().nativeElement;
    const arrayBuffer = await this.pdfFile().arrayBuffer();
    const pdf = await pdfjsLib.getDocument({ data: arrayBuffer }).promise;
    const page = await pdf.getPage(1);

    const maxWidth = 680;
    const viewport = page.getViewport({ scale: 1 });
    this.scale = Math.min(maxWidth / viewport.width, 1.5);
    const scaled = page.getViewport({ scale: this.scale });

    canvas.width = Math.round(scaled.width);
    canvas.height = Math.round(scaled.height);
    this.pageW.set(canvas.width);
    this.pageH.set(canvas.height);

    const ctx = canvas.getContext('2d')!;
    await page.render({ canvasContext: ctx, canvas, viewport: scaled }).promise;

    const position = this.initialPosition();
    if (position && this.isValidPosition(position)) {
      const width = Math.round(position.width * canvas.width);
      const height = Math.round(position.height * canvas.height);

      this.boxW.set(this.clamp(width, 40, canvas.width));
      this.boxH.set(this.clamp(height, 40, canvas.height));
      this.boxX.set(this.clamp(position.x * canvas.width, 0, canvas.width - this.boxW()));
      this.boxY.set(this.clamp(position.y * canvas.height, 0, canvas.height - this.boxH()));
      this.boxAngle.set(this.normalizeRotation(position.rotation));
      return;
    }

    const defaultW = Math.round(232 * this.scale);
    const defaultH = Math.round(108 * this.scale);
    const defaultMargin = Math.round(24 * this.scale);
    this.boxW.set(defaultW);
    this.boxH.set(defaultH);
    this.boxX.set(canvas.width - defaultW - defaultMargin);
    this.boxY.set(canvas.height - defaultH - defaultMargin);
    this.boxAngle.set(0);
  }

  protected onBoxPointerDown(e: PointerEvent) {
    if ((e.target as HTMLElement).dataset['handle']) return;
    e.preventDefault();
    const startPointer = this.getLocalPoint(e);
    const startOffsetX = startPointer.x - this.boxX();
    const startOffsetY = startPointer.y - this.boxY();

    const onMove = (mv: PointerEvent) => {
      const point = this.getLocalPoint(mv);
      this.boxX.set(this.clamp(point.x - startOffsetX, 0, this.pageW() - this.boxW()));
      this.boxY.set(this.clamp(point.y - startOffsetY, 0, this.pageH() - this.boxH()));
    };
    const onUp = () => {
      window.removeEventListener('pointermove', onMove);
      window.removeEventListener('pointerup', onUp);
    };
    window.addEventListener('pointermove', onMove);
    window.addEventListener('pointerup', onUp);
  }

  protected onHandlePointerDown(e: PointerEvent, handleId: HandleId) {
    e.preventDefault();
    e.stopPropagation();
    const startPointer = this.getLocalPoint(e);
    const startX = this.boxX();
    const startY = this.boxY();
    const startW = this.boxW();
    const startH = this.boxH();
    const minSize = 40;

    const onMove = (mv: PointerEvent) => {
      const point = this.getLocalPoint(mv);
      const dx = point.x - startPointer.x;
      const dy = point.y - startPointer.y;

      if (handleId.includes('e')) {
        this.boxW.set(this.clamp(startW + dx, minSize, this.pageW() - startX));
      }
      if (handleId.includes('s')) {
        this.boxH.set(this.clamp(startH + dy, minSize, this.pageH() - startY));
      }
      if (handleId.includes('w')) {
        const newW = this.clamp(startW - dx, minSize, startX + startW);
        this.boxX.set(this.clamp(startX + startW - newW, 0, this.pageW() - newW));
        this.boxW.set(newW);
      }
      if (handleId.includes('n')) {
        const newH = this.clamp(startH - dy, minSize, startY + startH);
        this.boxY.set(this.clamp(startY + startH - newH, 0, this.pageH() - newH));
        this.boxH.set(newH);
      }
    };
    const onUp = () => {
      window.removeEventListener('pointermove', onMove);
      window.removeEventListener('pointerup', onUp);
    };
    window.addEventListener('pointermove', onMove);
    window.addEventListener('pointerup', onUp);
  }

  protected onRotatePointerDown(e: PointerEvent) {
    e.preventDefault();
    e.stopPropagation();

    const onMove = (mv: PointerEvent) => {
      const point = this.getLocalPoint(mv);
      const centerX = this.boxX() + this.boxW() / 2;
      const centerY = this.boxY() + this.boxH() / 2;
      const angle = Math.atan2(point.y - centerY, point.x - centerX);
      this.boxAngle.set(Math.round(((angle * 180) / Math.PI + 90 + 360) % 360));
    };
    const onUp = () => {
      window.removeEventListener('pointermove', onMove);
      window.removeEventListener('pointerup', onUp);
    };
    window.addEventListener('pointermove', onMove);
    window.addEventListener('pointerup', onUp);
  }

  protected handleClass(h: HandleId): string {
    const base = 'absolute w-3 h-3 bg-white border border-blue-500 rounded-sm z-10 ';
    const map: Record<HandleId, string> = {
      n: 'cursor-n-resize  -top-1.5 left-1/2 -translate-x-1/2',
      s: 'cursor-s-resize  -bottom-1.5 left-1/2 -translate-x-1/2',
      e: 'cursor-e-resize  -right-1.5 top-1/2 -translate-y-1/2',
      w: 'cursor-w-resize  -left-1.5 top-1/2 -translate-y-1/2',
      nw: 'cursor-nw-resize -top-1.5 -left-1.5',
      ne: 'cursor-ne-resize -top-1.5 -right-1.5',
      sw: 'cursor-sw-resize -bottom-1.5 -left-1.5',
      se: 'cursor-se-resize -bottom-1.5 -right-1.5',
    };
    return base + map[h];
  }

  protected apply() {
    this.applied.emit({
      x: this.clamp(this.normX(), 0, 1),
      y: this.clamp(this.normY(), 0, 1),
      width: this.clamp(this.normW(), 0, 1),
      height: this.clamp(this.normH(), 0, 1),
      rotation: this.normalizeRotation(this.boxAngle()),
    });
    this.close();
  }

  protected cancel() {
    this.applied.emit(null);
    this.close();
  }

  protected close() {
    this.visibleChange.emit(false);
  }

  private getLocalPoint(e: PointerEvent): { x: number; y: number } {
    const rect = this.previewRef().nativeElement.getBoundingClientRect();
    return {
      x: e.clientX - rect.left,
      y: e.clientY - rect.top,
    };
  }

  private clamp(value: number, min: number, max: number): number {
    return Math.min(Math.max(value, min), max);
  }

  private normalizeRotation(rotation: number): number {
    const normalized = rotation % 360;
    return normalized < 0 ? normalized + 360 : normalized;
  }

  private isValidPosition(position: StampPosition): boolean {
    return Number.isFinite(position.x)
      && Number.isFinite(position.y)
      && Number.isFinite(position.width)
      && Number.isFinite(position.height)
      && Number.isFinite(position.rotation)
      && position.x >= 0
      && position.x <= 1
      && position.y >= 0
      && position.y <= 1
      && position.width > 0
      && position.width <= 1
      && position.height > 0
      && position.height <= 1;
  }
}
