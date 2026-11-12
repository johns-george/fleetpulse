import { Injectable, inject } from '@angular/core';
import { HttpClient, HttpParams } from '@angular/common/http';
import { Observable } from 'rxjs';
import { API_BASE_URL } from './api.config';
import { Device, DeviceDetail, DeviceSession, LocateResponse, PagedResult } from './models';

@Injectable({ providedIn: 'root' })
export class DeviceService {
  private readonly http = inject(HttpClient);

  list(search = '', status = '', page = 1, pageSize = 10): Observable<PagedResult<Device>> {
    let params = new HttpParams().set('page', page).set('pageSize', pageSize);

    if (search.trim()) {
      params = params.set('search', search.trim());
    }

    if (status) {
      params = params.set('status', status);
    }

    return this.http.get<PagedResult<Device>>(`${API_BASE_URL}/api/devices`, { params });
  }

  getDevice(id: number): Observable<DeviceDetail> {
    return this.http.get<DeviceDetail>(`${API_BASE_URL}/api/devices/${id}`);
  }

  lock(id: number): Observable<Device> {
    return this.http.post<Device>(`${API_BASE_URL}/api/devices/${id}/lock`, {});
  }

  locate(id: number): Observable<LocateResponse> {
    return this.http.post<LocateResponse>(`${API_BASE_URL}/api/devices/${id}/locate`, {});
  }

  checkout(id: number, workerName: string): Observable<Device> {
    return this.http.post<Device>(`${API_BASE_URL}/api/devices/${id}/checkout`, { workerName });
  }

  checkin(id: number): Observable<Device> {
    return this.http.post<Device>(`${API_BASE_URL}/api/devices/${id}/checkin`, {});
  }

  forceEndSession(id: number): Observable<Device> {
    return this.http.post<Device>(`${API_BASE_URL}/api/devices/${id}/force-end-session`, {});
  }

  sessions(id: number): Observable<DeviceSession[]> {
    return this.http.get<DeviceSession[]>(`${API_BASE_URL}/api/devices/${id}/sessions`);
  }
}
