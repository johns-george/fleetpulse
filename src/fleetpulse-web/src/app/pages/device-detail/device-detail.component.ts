import { Component, OnInit, inject, signal } from '@angular/core';
import { DatePipe, DecimalPipe } from '@angular/common';
import { ActivatedRoute, RouterLink } from '@angular/router';
import { HttpErrorResponse } from '@angular/common/http';
import { DeviceService } from '../../core/device.service';
import { DeviceDetail } from '../../core/models';
import { ToastService } from '../../core/toast.service';
import { BatteryIndicatorComponent } from '../../shared/battery-indicator.component';
import { DeviceMapComponent } from '../../shared/device-map.component';

@Component({
  selector: 'app-device-detail',
  standalone: true,
  imports: [DatePipe, DecimalPipe, RouterLink, BatteryIndicatorComponent, DeviceMapComponent],
  templateUrl: './device-detail.component.html',
  styleUrl: './device-detail.component.css'
})
export class DeviceDetailComponent implements OnInit {
  private readonly route = inject(ActivatedRoute);
  private readonly deviceService = inject(DeviceService);
  private readonly toast = inject(ToastService);

  readonly device = signal<DeviceDetail | null>(null);
  readonly loading = signal(true);
  readonly notFound = signal(false);
  readonly error = signal<string | null>(null);
  readonly lockPending = signal(false);
  readonly locatePending = signal(false);

  private deviceId = 0;

  ngOnInit(): void {
    const id = Number(this.route.snapshot.paramMap.get('id'));

    if (!Number.isInteger(id) || id <= 0) {
      this.loading.set(false);
      this.notFound.set(true);
      return;
    }

    this.deviceId = id;
    this.load();
  }

  load(): void {
    this.loading.set(true);
    this.error.set(null);

    this.deviceService.getDevice(this.deviceId).subscribe({
      next: (device) => {
        this.device.set(device);
        this.loading.set(false);
      },
      error: (error: HttpErrorResponse) => {
        this.loading.set(false);

        if (error.status === 404) {
          this.notFound.set(true);
        } else {
          this.error.set(
            error.status === 0
              ? 'Could not reach the API. Make sure it is running on port 5080.'
              : 'Could not load this device.'
          );
        }
      }
    });
  }

  lock(): void {
    const device = this.device();
    if (!device || this.lockPending()) {
      return;
    }

    this.lockPending.set(true);

    this.deviceService.lock(device.id).subscribe({
      next: (updated) => {
        this.device.update((current) =>
          current ? { ...current, isLocked: updated.isLocked } : current
        );
        this.lockPending.set(false);
        this.toast.success(`Lock command sent to ${updated.name}.`);
      },
      error: () => {
        this.lockPending.set(false);
        this.toast.error('Could not lock this device.');
      }
    });
  }

  locate(): void {
    const device = this.device();
    if (!device || this.locatePending()) {
      return;
    }

    this.locatePending.set(true);

    this.deviceService.locate(device.id).subscribe({
      next: (response) => {
        this.locatePending.set(false);
        this.toast.success(response.message);
      },
      error: () => {
        this.locatePending.set(false);
        this.toast.error('Could not send the locate command.');
      }
    });
  }
}
