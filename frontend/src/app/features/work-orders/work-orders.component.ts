import {FleetGridDirective} from '../../shared/fleet-grid.directive';
import {FleetDateComponent} from '../../shared/fleet-date.component';
import {Component,OnInit} from '@angular/core';
import {CommonModule} from '@angular/common';
import {RouterLink} from '@angular/router';
import {HttpClient} from '@angular/common/http';

@Component({selector:'app-work-orders',standalone:true,imports:[FleetGridDirective,FleetDateComponent,CommonModule,RouterLink],template:`
<section class="page">
 <div class="title"><div><h2>Work Orders</h2><p>Track each service job, assigned supervisor and work progress.</p></div></div>
 <p class="hint">Additional work stays under the same Job Card. Select Details for task counts, bay, costs and completion date.</p>
 <p *ngIf="loading" role="status">Loading work orders…</p><p *ngIf="error" role="alert">{{error}} <button (click)="load()">Retry</button></p>
 <div class="card"><div class="fleet-grid-scroll" role="region" aria-label="Work orders" tabindex="0">
 <table fleetGrid><thead><tr><th>Work Order</th><th>Vehicle</th><th>Service Type</th><th>Opened On</th><th>Supervisor</th><th>Status</th><th>Action</th></tr></thead><tbody>
 <ng-container *ngFor="let x of rows">
  <tr><td><b>{{x.workOrderNumber}}</b></td><td>{{x.vehicle}}</td><td>{{x.eventType||'Maintenance'}}</td><td class="date-cell"><app-fleet-date [value]="x.openedAt"></app-fleet-date></td><td>{{x.assignedSupervisor||'Unassigned'}}</td><td class="work-status"><span class="state" [class.waiting]="x.status==='Awaiting Assignment'">{{x.status}}</span></td><td><div class="work-actions"><a [routerLink]="['/service-workspace',x.id]">Open Workspace</a><a [href]="'/api/job-cards/'+x.id+'/report'" target="_blank" rel="noopener">Work Report</a><button (click)="toggleDetails(x)" [attr.aria-expanded]="expandedId===x.id" [attr.aria-controls]="'work-details-'+x.id">{{expandedId===x.id?'Hide Details':'Details'}}</button></div></td></tr>
  <tr class="fleet-detail-row" *ngIf="expandedId===x.id"><td colspan="7"><section class="work-details" [id]="'work-details-'+x.id" [attr.aria-label]="'Details for '+x.workOrderNumber">
   <div><span>Service Event</span><b>{{x.eventNumber||'—'}}</b></div><div><span>Tasks</span><b>{{x.taskCount}}</b></div><div><span>Open Defects</span><b>{{x.openDefects}}</b></div><div><span>Priority</span><b>{{x.priority}}</b></div><div><span>Bay</span><b>{{x.bay||'Not allocated'}}</b></div>
   <div><span>Completed On</span><app-fleet-date [value]="x.completedAt"></app-fleet-date></div><div><span>Supervisor Assigned On</span><app-fleet-date [value]="x.supervisorAssignedAt"></app-fleet-date></div>
   <div><span>Total Cost</span><b *ngIf="x.totalCost!==undefined">₹{{x.totalCost|number:'1.0-0'}}</b><small *ngIf="x.totalCost===undefined">{{x.costError||'Loading…'}}</small></div>
   <div class="request-refs"><span>Maintenance Requests</span><b>{{x.requestRefs?.join(', ')||x.requestError||(x.refsLoaded?'None linked':'Loading…')}}</b></div>
  </section></td></tr>
 </ng-container>
 <tr *ngIf="!loading&&!error&&!rows.length"><td colspan="7" class="empty">No Work Orders.</td></tr>
 </tbody></table></div></div>
</section>`,styles:[`
.title p,.hint{color:#64748b}.hint{font-size:12px;margin:0 0 16px}.card{padding:12px}
.work-status{min-width:130px}.state{display:inline-block;padding:5px 8px;border-radius:8px;background:#edf5ff;line-height:1.5}.state.waiting{background:#fff4e7;color:#9a4b0c}
.work-actions{display:flex;flex-direction:column;align-items:flex-start;gap:2px}.work-actions button{font:inherit;cursor:pointer}
.work-details{display:grid;grid-template-columns:repeat(4,minmax(0,1fr));gap:18px;padding:18px;background:#f7f9fc;border:1px solid #dfe7f0;border-radius:8px;margin:0 0 12px}
.work-details span{display:block;color:#64748b;font-size:11px;margin-bottom:6px}.work-details b{font-size:12px;font-weight:600}.request-refs{grid-column:1/-1}
@media(max-width:1100px){.work-details{grid-template-columns:repeat(2,minmax(0,1fr))}.work-actions{flex-direction:row;gap:16px}}
@media(max-width:600px){.work-details{grid-template-columns:1fr}.card{padding:8px}}
`]})
export class WorkOrdersComponent implements OnInit {
 rows:any[]=[];expandedId='';loading=true;error='';
 constructor(private h:HttpClient){}
 ngOnInit(){this.load()}
 load(){this.loading=true;this.error='';this.h.get<any[]>('/api/work-orders').subscribe({next:x=>{this.rows=x;this.loading=false},error:()=>{this.error='Unable to load work orders.';this.loading=false}})}
 toggleDetails(row:any){
  this.expandedId=this.expandedId===row.id?'':row.id;
  if(!this.expandedId)return;
  if(row.totalCost===undefined&&!row.costLoading){row.costLoading=true;this.h.get<any>('/api/work-orders/'+row.id+'/costs').subscribe({next:c=>{row.totalCost=c.total;row.costLoading=false},error:()=>{row.costLoading=false;row.costError='Unable to load cost'}})}
  if(!row.refsLoaded&&!row.refsLoading){row.refsLoading=true;this.h.get<any[]>('/api/tasks?jobCardId='+row.id).subscribe({next:tasks=>{const refs=new Set<string>();for(const task of tasks){const m=String(task.description||'').match(/^(MR-[^ ·]+)/);if(m)refs.add(m[1])}row.requestRefs=Array.from(refs);row.refsLoaded=true;row.refsLoading=false},error:()=>{row.refsLoading=false;row.requestError='Unable to load request references'}})}
 }
}
