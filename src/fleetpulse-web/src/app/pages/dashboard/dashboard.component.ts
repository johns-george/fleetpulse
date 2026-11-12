import { Component, DestroyRef, OnInit, computed, inject, signal } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { DatePipe } from '@angular/common';
import { HttpErrorResponse } from '@angular/common/http';
import { ChartConfiguration, ChartData } from 'chart.js';
import { BaseChartDirective } from 'ng2-charts';
import { merge, throttleTime } from 'rxjs';
import { DashboardService } from '../../core/dashboard.service';
import { DashboardSummary } from '../../core/models';
import { RealtimeService } from '../../core/realtime.service';

@Component({
  selector: 'app-dashboard',
  standalone: true,
  imports: [DatePipe, BaseChartDirective],
  templateUrl: './dashboard.component.html',
  styleUrl: './dashboard.component.css'
})
export class DashboardComponent implements OnInit {
  private readonly dashboardService = inject(DashboardService);
  private readonly realtime = inject(RealtimeService);
  private readonly destroyRef = inject(DestroyRef);

  readonly summary = signal<DashboardSummary | null>(null);
  readonly loading = signal(true);
  readonly error = signal<string | null>(null);
  readonly lastUpdated = signal<Date | null>(null);
  readonly connectionState = this.realtime.state;

  readonly doughnutData = computed<ChartData<'doughnut'>>(() => {
    const summary = this.summary();

    return {
      labels: ['Online', 'Offline'],
      datasets: [
        {
          data: [summary?.online ?? 0, summary?.offline ?? 0],
          backgroundColor: ['#16a34a', '#cbd5e1'],
          borderWidth: 0,
          hoverOffset: 6
        }
      ]
    };
  });

  readonly doughnutOptions: ChartConfiguration<'doughnut'>['options'] = {
    responsive: true,
    maintainAspectRatio: false,
    cutout: '70%',
    plugins: {
      legend: {
        position: 'bottom',
        labels: { usePointStyle: true, boxWidth: 8, padding: 16 }
      }
    }
  };

  readonly barData = computed<ChartData<'bar'>>(() => {
    const platforms = this.summary()?.platforms ?? [];

    return {
      labels: platforms.map((item) => item.platform),
      datasets: [
        {
          data: platforms.map((item) => item.count),
          backgroundColor: ['#2563eb', '#7c3aed', '#f59e0b'],
          borderRadius: 6,
          maxBarThickness: 56
        }
      ]
    };
  });

  readonly barOptions: ChartConfiguration<'bar'>['options'] = {
    responsive: true,
    maintainAspectRatio: false,
    plugins: { legend: { display: false } },
    scales: {
      x: { grid: { display: false }, border: { display: false } },
      y: {
        beginAtZero: true,
        ticks: { precision: 0 },
        grid: { color: '#eef2f7' },
        border: { display: false }
      }
    }
  };

  ngOnInit(): void {
    this.loadSummary();

    void this.realtime.connect();

    merge(this.realtime.deviceUpdated$, this.realtime.alertCreated$)
      .pipe(
        throttleTime(1200, undefined, { leading: true, trailing: true }),
        takeUntilDestroyed(this.destroyRef)
      )
      .subscribe(() => this.loadSummary(true));
  }

  loadSummary(silent = false): void {
    if (!silent) {
      this.loading.set(true);
    }

    this.error.set(null);

    this.dashboardService.getSummary().subscribe({
      next: (summary) => {
        this.summary.set(summary);
        this.lastUpdated.set(new Date());
        this.loading.set(false);
      },
      error: (error: HttpErrorResponse) => {
        this.loading.set(false);
        this.error.set(
          error.status === 0
            ? 'Could not reach the API. Make sure it is running on port 5080.'
            : 'Could not load the dashboard summary.'
        );
      }
    });
  }
}
