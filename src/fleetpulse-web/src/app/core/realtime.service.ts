import { Injectable, inject, signal } from '@angular/core';
import { HubConnection, HubConnectionBuilder, LogLevel } from '@microsoft/signalr';
import { Subject } from 'rxjs';
import { API_BASE_URL } from './api.config';
import { AuthService } from './auth.service';
import { Alert, CommandAcknowledgement, Device } from './models';

export type RealtimeState = 'disconnected' | 'connecting' | 'connected';

const RETRY_DELAY_MS = 5000;

/**
 * Owns the single SignalR connection to /hubs/devices and exposes the
 * device update and alert streams the UI subscribes to.
 */
@Injectable({ providedIn: 'root' })
export class RealtimeService {
  private readonly auth = inject(AuthService);

  private hub: HubConnection | null = null;
  private retryHandle: ReturnType<typeof setTimeout> | null = null;

  readonly state = signal<RealtimeState>('disconnected');
  readonly deviceUpdated$ = new Subject<Device>();
  readonly alertCreated$ = new Subject<Alert>();
  readonly commandAcknowledged$ = new Subject<CommandAcknowledgement>();

  async connect(): Promise<void> {
    if (this.hub || !this.auth.getToken()) {
      return;
    }

    const hub = new HubConnectionBuilder()
      .withUrl(`${API_BASE_URL}/hubs/devices`, {
        accessTokenFactory: () => this.auth.getToken() ?? ''
      })
      .withAutomaticReconnect()
      .configureLogging(LogLevel.Warning)
      .build();

    hub.on('DeviceUpdated', (device: Device) => this.deviceUpdated$.next(device));
    hub.on('AlertCreated', (alert: Alert) => this.alertCreated$.next(alert));
    hub.on('CommandAcknowledged', (ack: CommandAcknowledgement) =>
      this.commandAcknowledged$.next(ack)
    );
    hub.onreconnecting(() => this.state.set('connecting'));
    hub.onreconnected(() => this.state.set('connected'));
    hub.onclose(() => {
      this.hub = null;
      this.state.set('disconnected');

      if (this.auth.isAuthenticated()) {
        this.scheduleReconnect();
      }
    });

    this.hub = hub;
    this.state.set('connecting');

    try {
      await hub.start();
      this.state.set('connected');
    } catch {
      this.hub = null;
      this.state.set('disconnected');
      await hub.stop().catch(() => undefined);
      this.scheduleReconnect();
    }
  }

  async disconnect(): Promise<void> {
    if (this.retryHandle) {
      clearTimeout(this.retryHandle);
      this.retryHandle = null;
    }

    const hub = this.hub;
    this.hub = null;
    this.state.set('disconnected');

    if (hub) {
      await hub.stop().catch(() => undefined);
    }
  }

  private scheduleReconnect(): void {
    if (this.retryHandle) {
      return;
    }

    this.retryHandle = setTimeout(() => {
      this.retryHandle = null;
      void this.connect();
    }, RETRY_DELAY_MS);
  }
}
