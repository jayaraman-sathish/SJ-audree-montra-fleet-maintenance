import { Component } from '@angular/core';
import { CommonModule } from '@angular/common';

@Component({
  selector: 'app-root',
  standalone: true,
  imports: [CommonModule],
  templateUrl: './app.component.html',
  styleUrl: './app.component.css'
})
export class AppComponent {
  kpis = [
    ['Total Vehicles','1,248'], ['In Service','1,102'], ['Under Maintenance','96'],
    ['Off-hire','50'], ['Appointments Today','18'], ['Breakdowns','7'], ['PM Overdue','12'], ['SLA Breaches','3']
  ];
}
