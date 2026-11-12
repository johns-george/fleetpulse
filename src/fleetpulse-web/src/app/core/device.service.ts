import { Injectable, inject } from '@angular/core';
import { HttpClient, HttpParams } from '@angular/common/http';
import { Observable } from 'rxjs';
import { API_BASE_URL } from './api.config';
import { Device, DeviceDetail, LocateResponse } from './models';

@Injectable({ providedIn: 'root' })
export class DeviceService {
  private readonly http = inject(HttpClient);

  list(search = '', status = ''): Observable<Device[]> {
    let params = new HttpParams();

    if (search.trim()) {
      params = params.set('search', search.trim());
    }

    if (status) {
      params = params.set('status', status);
    }

    return this.http.get<Device[]>(`${API_BASE_URL}/api/devices`, { params });
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
}
