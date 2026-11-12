import { Component, inject } from '@angular/core';
import { ToastService } from '../core/toast.service';

@Component({
  selector: 'app-toast',
  standalone: true,
  template: `
    <div class="toast-stack" aria-live="polite" aria-atomic="false">
      @for (toast of toastService.toasts(); track toast.id) {
        <div class="toast" [class.error]="toast.tone === 'error'">
          <span class="icon" aria-hidden="true">{{ toast.tone === 'success' ? '✓' : '!' }}</span>
          <span class="message">{{ toast.message }}</span>
          <button type="button" class="close" aria-label="Dismiss" (click)="toastService.dismiss(toast.id)">
            ×
          </button>
        </div>
      }
    </div>
  `,
  styles: [
    `
      .toast-stack {
        position: fixed;
        right: 20px;
        bottom: 20px;
        z-index: 1000;
        display: flex;
        flex-direction: column;
        gap: 10px;
        max-width: min(360px, calc(100vw - 40px));
      }

      .toast {
        display: flex;
        align-items: flex-start;
        gap: 10px;
        padding: 12px 14px;
        border-radius: 10px;
        background: #0f172a;
        color: #f8fafc;
        font-size: 13px;
        box-shadow: 0 12px 30px rgba(15, 23, 42, 0.25);
        animation: slide-in 0.18s ease-out;
      }

      .toast.error {
        background: #991b1b;
      }

      .icon {
        display: grid;
        place-items: center;
        flex: 0 0 auto;
        width: 18px;
        height: 18px;
        margin-top: 1px;
        border-radius: 50%;
        background: rgba(255, 255, 255, 0.18);
        font-size: 11px;
        font-weight: 700;
      }

      .message {
        flex: 1;
        line-height: 1.4;
      }

      .close {
        flex: 0 0 auto;
        border: none;
        background: none;
        color: inherit;
        opacity: 0.7;
        font-size: 18px;
        line-height: 1;
        cursor: pointer;
        padding: 0;
      }

      .close:hover {
        opacity: 1;
      }

      @keyframes slide-in {
        from {
          transform: translateY(8px);
          opacity: 0;
        }
        to {
          transform: translateY(0);
          opacity: 1;
        }
      }
    `
  ]
})
export class ToastComponent {
  readonly toastService = inject(ToastService);
}
