import {Component,OnInit} from '@angular/core';
import {CommonModule} from '@angular/common';
import {FormsModule} from '@angular/forms';
import {RouterLink} from '@angular/router';
import {HttpClient} from '@angular/common/http';

@Component({selector:'app-service',standalone:true,imports:[CommonModule,FormsModule,RouterLink],template:`
<section class="page"><h2>Service Execution</h2><p class="muted">Operational cockpit for vehicles currently in service.</p>
 <div class="filters"><label>Search<input [(ngModel)]="search" placeholder="Event, breakdown, job card or vehicle"></label><label>Type<select [(ngModel)]="type"><option value="">All types</option><option>Breakdown</option><option>PM</option><option>Maintenance</option></select></label><label>Status<select [(ngModel)]="status"><option value="">All statuses</option><option>Awaiting Assignment</option><option>Assigned</option><option>In Progress</option><option>Open</option></select></label></div>
 <div class="card"><table><thead><tr><th>Event</th><th>Source</th><th>Vehicle</th><th>Type</th><th>Job Card</th><th>Bay</th><th>Technician</th><th>Status</th><th></th></tr></thead><tbody><tr *ngFor="let x of filtered()"><td><b>{{x.eventNo}}</b></td><td><a *ngIf="x.breakdownNumber" routerLink="/breakdown">{{x.breakdownNumber}}</a><span *ngIf="!x.breakdownNumber">—</span></td><td>{{x.vehicle}}</td><td>{{x.type}}</td><td>{{x.jobCard}}</td><td>{{x.bay||'—'}}</td><td>{{x.technician||'Unassigned'}}</td><td><span class="tag" [class.waiting]="x.status==='Awaiting Assignment'">{{x.status}}</span></td><td><a *ngIf="x.jobCardId" class="btn" [routerLink]="['/service-workspace',x.jobCardId]">Open Workspace</a></td></tr><tr *ngIf="!filtered().length"><td colspan="9" class="empty">No service events match the selected filters.</td></tr></tbody></table></div>
</section>`,styles:[`
.page{padding:26px}.muted{color:#64748b}.filters{display:grid;grid-template-columns:2fr 1fr 1fr;gap:10px;margin:16px 0}.filters label{display:grid;gap:5px;color:#475569;font-size:12px}.filters input,.filters select{min-height:44px;padding:8px;border:1px solid #cbd5e1;border-radius:7px;background:#fff}.card{background:#fff;border:1px solid #e2e8f0;border-radius:12px;padding:14px;overflow-x:auto}table{width:100%;border-collapse:collapse}th,td{padding:10px;border-bottom:1px solid #e5e7eb;text-align:left;font-size:12px}th{color:#475569}td a:not(.btn){color:#1266d5;font-weight:700}.btn{display:inline-flex;min-height:42px;align-items:center;padding:7px 12px;border-radius:7px;background:#1266d5;color:#fff;text-decoration:none}.tag{display:inline-block;padding:5px 8px;border-radius:12px;background:#eaf3ff}.tag.waiting{background:#fff7ed;color:#9a3412}.empty{text-align:center;color:#64748b;padding:24px}
@media(max-width:800px){.page{padding:14px}.filters{grid-template-columns:1fr}table thead{display:none}table tr{display:grid;grid-template-columns:1fr 1fr;border:1px solid #dbe4ef;border-radius:10px;margin-bottom:10px;padding:10px}table td{display:block;border:0;padding:5px}.btn{width:100%;box-sizing:border-box;justify-content:center}}@media(max-width:500px){.page{padding:10px}table tr{grid-template-columns:1fr}}
`]})
export class ServiceComponent implements OnInit{
 rows:any[]=[];search='';type='';status='';
 constructor(private h:HttpClient){}
 ngOnInit(){this.h.get<any[]>('/api/service-events/active').subscribe(x=>this.rows=x)}
 filtered(){const q=this.search.trim().toLowerCase();return this.rows.filter(x=>(!q||[x.eventNo,x.breakdownNumber,x.jobCard,x.vehicle].some(v=>String(v||'').toLowerCase().includes(q)))&&(!this.type||x.type===this.type)&&(!this.status||x.status===this.status))}
}
