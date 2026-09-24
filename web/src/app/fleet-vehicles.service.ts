import { HttpClient, HttpParams } from '@angular/common/http';
import { Injectable } from '@angular/core';
import { Observable } from 'rxjs';

export interface FleetVehicle {
  id: string;
  registrationPlate: string;
  fuelType: string;
  make: string | null;
  model: string | null;
  isArchived: boolean;
}

export interface FleetVehicleRequest {
  registrationPlate: string;
  fuelType: string;
  make: string | null;
  model: string | null;
}

@Injectable({ providedIn: 'root' })
export class FleetVehiclesService {
  constructor(private readonly http: HttpClient) {}

  getVehicles(search: string, includeArchived: boolean): Observable<FleetVehicle[]> {
    const params = new HttpParams()
      .set('search', search)
      .set('includeArchived', includeArchived);
    return this.http.get<FleetVehicle[]>('/api/cars', { params });
  }

  createVehicle(request: FleetVehicleRequest): Observable<FleetVehicle> {
    return this.http.post<FleetVehicle>('/api/cars', request);
  }

  updateVehicle(id: string, request: FleetVehicleRequest): Observable<FleetVehicle> {
    return this.http.put<FleetVehicle>(`/api/cars/${id}`, request);
  }

  archiveVehicle(id: string): Observable<FleetVehicle> {
    return this.http.post<FleetVehicle>(`/api/cars/${id}/archive`, {});
  }

  restoreVehicle(id: string): Observable<FleetVehicle> {
    return this.http.post<FleetVehicle>(`/api/cars/${id}/restore`, {});
  }
}