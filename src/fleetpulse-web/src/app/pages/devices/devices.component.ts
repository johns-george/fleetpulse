import { Component, DestroyRef, OnInit, inject, signal } from '@angular/core';
import { Router } from '@angular/router';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { DatePipe } from '@angular/common';
import { FormControl, ReactiveFormsModule } from '@angular/forms';
import { HttpErrorResponse } from '@angular/common/http';
import { BehaviorSubject, catchError, combineLatest, debounceTime, map, of, startWith, switchMap, tap } from 'rxjs';
import { Device, DeviceStatus } from '../../core/models';
import { DeviceService } from '../../core/device.service';
import { BatteryIndicatorComponent } from '../../shared/battery-indicator.component';

@Component({
  selector: 'app-devices',
  standalone: true,
  imports: [ReactiveFormsModule, DatePipe, BatteryIndicatorComponent],
  templateUrl: './devices.component.html',
  styleUrl: './devices.component.css'
})
export class DevicesComponent implements OnInit {
  private readonly deviceService = inject(DeviceService);
  private readonly router = inject(Router);
  private readonly destroyRef = inject(DestroyRef);
  private readonly refreshTrigger = new BehaviorSubject<void>(undefined);

  readonly search = new FormControl('', { nonNullable: true });
  readonly status = new FormControl<'' | DeviceStatus>('', { nonNullable: true });

  readonly devices = signal<Device[]>([]);
  readonly loading = signal(true);
  readonly error = signal<string | null>(null);
  readonly lastUpdated = signal<Date | null>(null);

  ngOnInit(): void {
    combineLatest([
      this.search.valueChanges.pipe(startWith('')),
      this.status.valueChanges.pipe(startWith('')),
      this.refreshTrigger
    ])
      .pipe(
        debounceTime(200),
        tap(() => {
          this.loading.set(true);
          this.error.set(null);
        }),
        switchMap(() =>
          this.deviceService.list(this.search.value, this.status.value).pipe(
            catchError((error: HttpErrorResponse) => {
              this.error.set(
                error.status === 0
                  ? 'Could not reach the API. Make sure it is running on port 5080.'
                  : 'Could not load devices. Please try again.'
              );
              return of<Device[]>([]);
            }),
            map((devices) => devices)
          )
        ),
        takeUntilDestroyed(this.destroyRef)
      )
      .subscribe((devices) => {
        this.devices.set(devices);
        this.loading.set(false);
        this.lastUpdated.set(new Date());
      });
  }

  retry(): void {
    this.refreshTrigger.next();
  }

  clearFilters(): void {
    this.search.setValue('');
    this.status.setValue('');
  }

  openDevice(id: number): void {
    void this.router.navigate(['/devices', id]);
  }
}
