import { HttpClient, HttpParams } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';

export interface FleetProgramRule {
  fuelType: string;
  percent: number;
}

export interface FleetProgram {
  id: string;
  name: string;
  activeFrom: string;
  activeTo: string | null;
  rules: FleetProgramRule[];
}

export interface ProgramAssignment {
  id: string;
  programId: string;
  startsAt: string;
  endsAt: string | null;
}

export interface ProgramsResponse {
  programs: FleetProgram[];
  currentAssignment: ProgramAssignment | null;
}

export interface ProgramComparison {
  fromUtc: string;
  toUtc: string;
  baseline: {
    netTotal: number;
    vatTotal: number;
    grossTotal: number;
  };
  programs: Array<{
    programId: string;
    programName: string;
    hypotheticalNetTotal: number;
    hypotheticalGrossTotal: number;
    netSavings: number;
    grossSavings: number;
  }>;
}

@Injectable({ providedIn: 'root' })
export class ProgramsService {
  private readonly http = inject(HttpClient);

  getPrograms(): Observable<ProgramsResponse> {
    return this.http.get<ProgramsResponse>('/api/programs');
  }

  getAssignments(): Observable<ProgramAssignment[]> {
    return this.http.get<ProgramAssignment[]>('/api/programs/assignments');
  }

  getComparison(from: string, to: string): Observable<ProgramComparison> {
    const params = new HttpParams().set('from', from).set('to', to);
    return this.http.get<ProgramComparison>('/api/programs/comparison', { params });
  }

  scheduleProgram(programId: string, startsAt: string): Observable<void> {
    return this.http.post<void>('/api/programs/assignments', { programId, startsAt });
  }

  scheduleEnd(endsAt: string): Observable<void> {
    return this.http.post<void>('/api/programs/assignments/end', { endsAt });
  }
}
