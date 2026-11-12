import { Component, DestroyRef, OnInit, inject, signal } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { DatePipe, DecimalPipe } from '@angular/common';
import { FormControl, ReactiveFormsModule } from '@angular/forms';
import { ActivatedRoute, RouterLink } from '@angular/router';
import { HttpErrorResponse } from '@angular/common/http';
import { DeviceService } from '../../core/device.service';
import { DeviceDetail, DeviceSession } from '../../core/models';
import { RealtimeService } from '../../core/realtime.service';
import { ToastService } from '../../core/toast.service';
import { BatteryIndicatorComponent } from '../../shared/battery-indicator.component';
import { DeviceMapComponent } from '../../shared/device-map.component';

@Component({
  selector: 'app-device-detail',
  standalone: true,
  imports: [
    DatePipe,
    DecimalPipe,
    ReactiveFormsModule,
    RouterLink,
    BatteryIndicatorComponent,
    DeviceMapComponent
  ],
  templateUrl: './device-detail.component.html',
  styleUrl: './device-detail.component.css'
})
export class DeviceDetailComponent implements OnInit {
  private readonly route = inject(ActivatedRoute);
  private readonly deviceService = inject(DeviceService);
  private readonly toast = inject(ToastService);
  private readonly realtime = inject(RealtimeService);
  private readonly destroyRef = inject(DestroyRef);

  readonly device = signal<DeviceDetail | null>(null);
  readonly loading = signal(true);
  readonly notFound = signal(false);
  readonly error = signal<string | null>(null);
  readonly lockPending = signal(false);
  readonly locatePending = signal(false);
  readonly sessionPending = signal(false);
  readonly sessions = signal<DeviceSession[]>([]);

  readonly workerName = new FormControl('', { nonNullable: true });

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

    this.realtime.commandAcknowledged$
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe((ack) => {
        if (ack.deviceId !== this.deviceId) {
          return;
        }

        const name = this.device()?.name ?? 'the device';
        this.toast.success(`${this.commandLabel(ack.command)} confirmed by ${name}.`);
      });
  }

  load(): void {
    this.loading.set(true);
    this.error.set(null);

    this.deviceService.getDevice(this.deviceId).subscribe({
      next: (device) => {
        this.device.set(device);
        this.loading.set(false);
        this.loadSessions();
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

  loadSessions(): void {
    this.deviceService.sessions(this.deviceId).subscribe({
      next: (sessions) => this.sessions.set(sessions),
      error: () => this.sessions.set([])
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

  checkout(): void {
    const device = this.device();
    const worker = this.workerName.value.trim();

    if (!device || this.sessionPending()) {
      return;
    }

    if (!worker) {
      this.toast.error('Enter a worker name to check out this device.');
      return;
    }

    this.sessionPending.set(true);

    this.deviceService.checkout(device.id, worker).subscribe({
      next: (updated) => {
        this.applyDevice(updated);
        this.workerName.setValue('');
        this.sessionPending.set(false);
        this.toast.success(`${updated.name} checked out to ${updated.currentWorker}.`);
        this.loadSessions();
      },
      error: (error: HttpErrorResponse) => {
        this.sessionPending.set(false);
        this.toast.error(
          error.status === 409
            ? 'This device is already checked out.'
            : 'Could not check out this device.'
        );
      }
    });
  }

  checkin(): void {
    this.endSession('checkin');
  }

  forceEnd(): void {
    this.endSession('force');
  }

  endedLabel(session: DeviceSession): string {
    if (!session.endedAt) {
      return 'Active';
    }

    return session.endedBy?.startsWith('admin:') ? 'Ended by admin' : 'Checked in';
  }

  private endSession(mode: 'checkin' | 'force'): void {
    const device = this.device();
    if (!device || this.sessionPending()) {
      return;
    }

    this.sessionPending.set(true);

    const request =
      mode === 'checkin'
        ? this.deviceService.checkin(device.id)
        : this.deviceService.forceEndSession(device.id);

    request.subscribe({
      next: (updated) => {
        this.applyDevice(updated);
        this.sessionPending.set(false);
        this.toast.success(
          mode === 'checkin' ? 'Device checked in.' : 'Session ended by an administrator.'
        );
        this.loadSessions();
      },
      error: (error: HttpErrorResponse) => {
        this.sessionPending.set(false);
        this.toast.error(
          error.status === 409
            ? 'This device has no active session.'
            : 'Could not end the session.'
        );
      }
    });
  }

  private commandLabel(command: string): string {
    switch (command.toLowerCase()) {
      case 'lock':
        return 'Lock';
      case 'unlock':
        return 'Unlock';
      case 'locate':
        return 'Locate';
      case 'checkout':
        return 'Check-out';
      case 'checkin':
        return 'Check-in';
      case 'end-session':
        return 'End session';
      default:
        return command;
    }
  }

  private applyDevice(updated: { currentWorker?: string | null; isLocked: boolean }): void {
    this.device.update((current) =>
      current
        ? { ...current, currentWorker: updated.currentWorker, isLocked: updated.isLocked }
        : current
    );
  }
}
