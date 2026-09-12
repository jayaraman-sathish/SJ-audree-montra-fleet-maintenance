import { Routes } from '@angular/router';
import { DashboardComponent } from './features/dashboard/dashboard.component';
import { VehicleComponent } from './features/vehicle/vehicle.component';
import { PmComponent } from './features/pm/pm.component';
import { ServiceComponent } from './features/service/service.component';

export const routes: Routes = [
  { path: '', component: DashboardComponent },
  { path: 'vehicle', component: VehicleComponent },
  { path: 'pm', component: PmComponent },
  { path: 'service', component: ServiceComponent },
  { path: '**', redirectTo: '' }
];
