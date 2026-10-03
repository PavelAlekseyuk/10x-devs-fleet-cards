import { Component, inject, OnInit } from '@angular/core';
import { ButtonComponent } from './shared/ui/button.component';
import { StatusBadgeComponent } from './shared/ui/status-badge.component';
import { AuthService } from './auth.service';
import { FleetVehicle, FleetVehicleRequest, FleetVehiclesService } from './fleet-vehicles.service';
import { LanguageService, TranslationKey } from './language.service';

interface VehicleDraft {
  id: string | null;
  registrationPlate: string;
  fuelType: string;
  make: string;
  model: string;
}

const fuelTranslationKeys: Record<string, TranslationKey> = {
  '95': 'fuel95',
  '98': 'fuel98',
  Diesel: 'fuelDiesel',
  '95+': 'fuel95Plus',
  '98+': 'fuel98Plus',
  'Diesel+': 'fuelDieselPlus',
};

@Component({
  selector: 'app-fleet-vehicles',
  standalone: true,
  imports: [ButtonComponent, StatusBadgeComponent],
  templateUrl: './fleet-vehicles.component.html',
  styleUrl: './fleet-vehicles.component.scss',
})
export class FleetVehiclesComponent implements OnInit {
  protected readonly auth = inject(AuthService);
  protected readonly language = inject(LanguageService);
  protected readonly fuelTypes = ['95', '98', 'Diesel', '95+', '98+', 'Diesel+'];
  protected vehicles: FleetVehicle[] = [];
  protected search = '';
  protected appliedSearch = '';
  protected showingArchived = false;
  protected loading = true;
  protected saving = false;
  protected error: string | null = null;
  protected formError: string | null = null;
  protected draft: VehicleDraft | null = null;

  private readonly vehiclesService = inject(FleetVehiclesService);

  ngOnInit(): void {
    this.loadVehicles();
  }

  protected get canWrite(): boolean {
    return this.auth.user()?.permission === 'ReadWrite';
  }

  protected get visibleVehicles(): FleetVehicle[] {
    return this.vehicles.filter((vehicle) => vehicle.isArchived === this.showingArchived);
  }

  protected fuelLabel(fuelType: string): string {
    const key = fuelTranslationKeys[fuelType];
    return key ? this.language.text(key) : fuelType;
  }

  protected onSearchInput(event: Event): void {
    this.search = (event.target as HTMLInputElement).value;
  }

  protected updateDraft(field: keyof Omit<VehicleDraft, 'id'>, event: Event): void {
    if (this.draft) {
      this.draft[field] = (event.target as HTMLInputElement).value;
    }
  }

  protected vehicleDescription(vehicle: FleetVehicle): string {
    return [vehicle.make, vehicle.model].filter((part) => part !== null && part !== '').join(' ')
      || this.language.text('vehicleDescriptionMissing');
  }

  protected setArchiveFilter(showArchived: boolean): void {
    this.showingArchived = showArchived;
    this.error = null;
    this.loadVehicles();
  }

  protected applySearch(): void {
    this.appliedSearch = this.search.trim();
    this.loadVehicles();
  }

  protected startCreate(): void {
    this.formError = null;
    this.draft = { id: null, registrationPlate: '', fuelType: '', make: '', model: '' };
  }

  protected startEdit(vehicle: FleetVehicle): void {
    this.formError = null;
    this.draft = {
      id: vehicle.id,
      registrationPlate: vehicle.registrationPlate,
      fuelType: vehicle.fuelType,
      make: vehicle.make ?? '',
      model: vehicle.model ?? '',
    };
  }

  protected cancelEdit(): void {
    this.draft = null;
    this.formError = null;
  }

  protected saveVehicle(): void {
    if (
      !this.canWrite
      || !this.draft
      || !this.draft.registrationPlate.trim()
      || !this.fuelTypes.includes(this.draft.fuelType)
    ) {
      this.formError = this.language.text('vehicleFormInvalid');
      return;
    }

    const request: FleetVehicleRequest = {
      registrationPlate: this.draft.registrationPlate.trim(),
      fuelType: this.draft.fuelType,
      make: this.draft.make.trim() || null,
      model: this.draft.model.trim() || null,
    };
    this.saving = true;
    this.formError = null;
    const save = this.draft.id
      ? this.vehiclesService.updateVehicle(this.draft.id, request)
      : this.vehiclesService.createVehicle(request);

    save.subscribe({
      next: () => {
        this.saving = false;
        this.draft = null;
        this.loadVehicles();
      },
      error: () => {
        this.saving = false;
        this.formError = this.language.text('vehicleSaveError');
      },
    });
  }

  protected archiveVehicle(vehicle: FleetVehicle): void {
    this.mutateVehicle(vehicle.id, this.vehiclesService.archiveVehicle(vehicle.id));
  }

  protected restoreVehicle(vehicle: FleetVehicle): void {
    this.mutateVehicle(vehicle.id, this.vehiclesService.restoreVehicle(vehicle.id));
  }

  private mutateVehicle(_id: string, request: ReturnType<FleetVehiclesService['archiveVehicle']>): void {
    this.error = null;
    request.subscribe({
      next: () => this.loadVehicles(),
      error: () => {
        this.error = this.language.text('vehicleMutationError');
      },
    });
  }

  private loadVehicles(): void {
    this.loading = true;
    this.error = null;
    this.vehiclesService.getVehicles(this.appliedSearch, this.showingArchived).subscribe({
      next: (vehicles) => {
        this.vehicles = vehicles;
        this.loading = false;
      },
      error: () => {
        this.loading = false;
        this.error = this.language.text('vehicleLoadError');
      },
    });
  }
}