import {
  AfterViewInit,
  Component,
  ElementRef,
  OnDestroy,
  ViewChild,
  effect,
  input
} from '@angular/core';
import * as L from 'leaflet';

/**
 * Small Leaflet map showing a device location. A styled div icon is used so
 * no bundler-specific image asset paths are needed.
 */
@Component({
  selector: 'app-device-map',
  standalone: true,
  template: `<div class="map" #mapHost></div>`,
  styles: [
    `
      :host {
        display: block;
      }

      .map {
        position: relative;
        z-index: 0;
        height: 280px;
        width: 100%;
        border-radius: 10px;
        border: 1px solid var(--border);
        overflow: hidden;
      }
    `
  ]
})
export class DeviceMapComponent implements AfterViewInit, OnDestroy {
  readonly latitude = input.required<number>();
  readonly longitude = input.required<number>();
  readonly label = input<string>('');

  @ViewChild('mapHost', { static: true })
  private mapHost!: ElementRef<HTMLDivElement>;

  private map?: L.Map;
  private marker?: L.Marker;

  constructor() {
    effect(() => {
      const latitude = this.latitude();
      const longitude = this.longitude();
      const label = this.label();

      if (!this.map || !this.marker) {
        return;
      }

      this.marker.setLatLng([latitude, longitude]);
      this.marker.setPopupContent(label || 'Device');
      this.map.panTo([latitude, longitude]);
    });
  }

  ngAfterViewInit(): void {
    const latitude = this.latitude();
    const longitude = this.longitude();

    this.map = L.map(this.mapHost.nativeElement, {
      center: [latitude, longitude],
      zoom: 14,
      scrollWheelZoom: false
    });

    L.tileLayer('https://{s}.tile.openstreetmap.org/{z}/{x}/{y}.png', {
      maxZoom: 19,
      attribution: '&copy; OpenStreetMap contributors'
    }).addTo(this.map);

    this.marker = L.marker([latitude, longitude], { icon: this.dotIcon() })
      .addTo(this.map)
      .bindPopup(this.label() || 'Device');
  }

  ngOnDestroy(): void {
    this.map?.remove();
    this.map = undefined;
    this.marker = undefined;
  }

  private dotIcon(): L.DivIcon {
    return L.divIcon({
      className: 'device-marker',
      html:
        '<span style="display:block;width:16px;height:16px;border-radius:50%;' +
        'background:#2563eb;border:3px solid #fff;' +
        'box-shadow:0 0 0 4px rgba(37,99,235,0.25)"></span>',
      iconSize: [16, 16],
      iconAnchor: [8, 8]
    });
  }
}
