import { Component, input } from '@angular/core';

/**
 * Compact battery gauge: red below 15%, amber below 40%, green otherwise.
 */
@Component({
  selector: 'app-battery-indicator',
  standalone: true,
  template: `
    <div
      class="battery"
      [class.low]="clamped() < 15"
      [class.medium]="clamped() >= 15 && clamped() < 40"
      [class.ok]="clamped() >= 40"
      [attr.aria-label]="'Battery ' + clamped() + ' percent'">
      <span class="track">
        <span class="fill" [style.width.%]="clamped()"></span>
      </span>
      <span class="value">{{ clamped() }}%</span>
    </div>
  `,
  styles: [
    `
      .battery {
        display: inline-flex;
        align-items: center;
        gap: 8px;
        min-width: 96px;
      }

      .track {
        position: relative;
        display: block;
        width: 48px;
        height: 8px;
        border-radius: 999px;
        background: #e2e8f0;
        overflow: hidden;
      }

      .fill {
        position: absolute;
        inset: 0 auto 0 0;
        border-radius: 999px;
        background: #16a34a;
        transition: width 0.3s ease;
      }

      .value {
        font-size: 12px;
        font-weight: 600;
        font-variant-numeric: tabular-nums;
        color: #334155;
      }

      .low .fill {
        background: #dc2626;
      }

      .low .value {
        color: #b91c1c;
      }

      .medium .fill {
        background: #d97706;
      }

      .medium .value {
        color: #b45309;
      }
    `
  ]
})
export class BatteryIndicatorComponent {
  readonly level = input.required<number>();

  clamped() {
    return Math.max(0, Math.min(100, Math.round(this.level())));
  }
}
