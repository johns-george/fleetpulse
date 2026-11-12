import { Component, DestroyRef, OnInit, computed, inject, signal } from '@angular/core';
import { Router } from '@angular/router';
import { takeUntilDestroyed, toObservable } from '@angular/core/rxjs-interop';
import { DatePipe } from '@angular/common';
import { FormControl, ReactiveFormsModule } from '@angular/forms';
import { HttpErrorResponse } from '@angular/common/http';
import {
  BehaviorSubject,
  catchError,
  combineLatest,
  debounceTime,
  distinctUntilChanged,
  of,
  startWith,
  switchMap,
  tap
} from 'rxjs';
import { Device, DeviceStatus, PagedResult } from '../../core/models';
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

  readonly pageSize = 10;
  readonly page = signal(1);
  readonly devices = signal<Device[]>([]);
  readonly totalCount = signal(0);
  readonly totalPages = computed(() => Math.max(1, Math.ceil(this.totalCount() / this.pageSize)));
  private readonly page$ = toObservable(this.page);
  readonly loading = signal(true);
  readonly error = signal<string | null>(null);
  readonly lastUpdated = signal<Date | null>(null);

  ngOnInit(): void {
    // Changing a filter always returns the user to the first page.
    this.search.valueChanges.pipe(takeUntilDestroyed(this.destroyRef)).subscribe(() => this.page.set(1));
    this.status.valueChanges.pipe(takeUntilDestroyed(this.destroyRef)).subscribe(() => this.page.set(1));

    combineLatest([
      this.search.valueChanges.pipe(startWith(''), debounceTime(300), distinctUntilChanged()),
      this.status.valueChanges.pipe(startWith(''), distinctUntilChanged()),
      this.page$,
      this.refreshTrigger
    ])
      .pipe(
        tap(() => {
          this.loading.set(true);
          this.error.set(null);
        }),
        switchMap(() =>
          this.deviceService.list(this.search.value, this.status.value, this.page(), this.pageSize).pipe(
            catchError((error: HttpErrorResponse) => {
              this.error.set(
                error.status === 0
                  ? 'Could not reach the API. Make sure it is running on port 5080.'
                  : 'Could not load devices. Please try again.'
              );
              return of<PagedResult<Device>>({
                items: [],
                page: this.page(),
                pageSize: this.pageSize,
                totalCount: 0
              });
            })
          )
        ),
        takeUntilDestroyed(this.destroyRef)
      )
      .subscribe((result) => {
        this.devices.set(result.items);
        this.totalCount.set(result.totalCount);
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

  previousPage(): void {
    if (this.page() > 1) {
      this.page.update((page) => page - 1);
    }
  }

  nextPage(): void {
    if (this.page() < this.totalPages()) {
      this.page.update((page) => page + 1);
    }
  }

  openDevice(id: number): void {
    void this.router.navigate(['/devices', id]);
  }
}
