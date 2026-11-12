export type Platform = 'Android' | 'iOS' | 'Windows';
export type DeviceStatus = 'Online' | 'Offline';

export interface Device {
  id: number;
  name: string;
  platform: Platform;
  osVersion: string;
  batteryLevel: number;
  latitude: number;
  longitude: number;
  isLocked: boolean;
  lastSeen: string;
  status: DeviceStatus;
  currentWorker?: string | null;
}

export interface PagedResult<T> {
  items: T[];
  page: number;
  pageSize: number;
  totalCount: number;
}

export interface Alert {
  id: number;
  deviceId: number;
  message: string;
  createdAt: string;
}

export interface DeviceDetail extends Device {
  alerts: Alert[];
}

export interface CommandAcknowledgement {
  deviceId: number;
  command: string;
  acknowledgedAt: string;
}

export interface DeviceSession {
  id: number;
  deviceId: number;
  workerName: string;
  startedAt: string;
  endedAt: string | null;
  endedBy: string | null;
}

export interface PlatformCount {
  platform: Platform;
  count: number;
}

export interface DashboardSummary {
  total: number;
  online: number;
  offline: number;
  lowBattery: number;
  platforms: PlatformCount[];
}

export interface LocateResponse {
  message: string;
}

export interface LoginResponse {
  token: string;
  username: string;
  expiresAtUtc: string;
}
