import { Routes } from '@angular/router';
import { DashboardComponent } from './features/dashboard/dashboard.component';
import { VehicleComponent } from './features/vehicle/vehicle.component';
import { PmComponent } from './features/pm/pm.component';
import { ServiceComponent } from './features/service/service.component';
import { BreakdownComponent } from './features/breakdown/breakdown.component';
import { JobCardComponent } from './features/job-card/job-card.component';
import { PartsComponent } from './features/parts/parts.component';
import { ReleaseComponent } from './features/release/release.component';
import { AvailabilityComponent } from './features/availability/availability.component';

export const routes: Routes = [
  { path: '', component: DashboardComponent },
  { path: 'vehicle', component: VehicleComponent },
  { path: 'pm', component: PmComponent },
  { path: 'service', component: ServiceComponent },
  { path: 'breakdown', component: BreakdownComponent },
  { path: 'job-card', component: JobCardComponent },
  { path: 'parts', component: PartsComponent },
  { path: 'release', component: ReleaseComponent },
  { path: 'availability', component: AvailabilityComponent },
  { path: '**', redirectTo: '' }
];
