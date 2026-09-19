import {SupervisorSelectComponent} from '../../shared/supervisor-select.component';
import {FleetGridDirective} from '../../shared/fleet-grid.directive';
import { FleetDateComponent } from '../../shared/fleet-date.component';
import {Component,OnInit} from '@angular/core';
import {CommonModule} from '@angular/common';
import {FormsModule} from '@angular/forms';
import {HttpClient} from '@angular/common/http';
import {Router} from '@angular/router';

@Component({selector:'app-appointments',standalone:true,imports:[SupervisorSelectComponent,FleetGridDirective,FleetDateComponent,CommonModule,FormsModule],template:`
<section class="page">
<div class="title"><div><h2>Appointments & Capacity</h2><p>Appointment = when and where the vehicle will visit. PM appointments originate from PM Obligations.</p></div><button type="button" class="btn btn-primary" (click)="openCreate()">+ Appointment</button></div>
<div class="info"><b>Planned PM:</b> schedule from <b>PM Obligations</b>. Repair/inspection/campaign visits can be created here. Technician/team assignment happens after check-in.</div>
<div class="capacity card"><div><b>Selected day capacity</b><span>{{capacityDate||'Select appointment date'}}</span></div><div *ngFor="let b of capacity?.bays"><b>{{b.bayCode}}</b><span>{{b.bookedHours}} h booked</span></div></div>

<div class="modal-backdrop" *ngIf="createOpen||checkOpen||startOpen"></div>
<div class="modal-card" *ngIf="createOpen">
 <div class="modal-head"><div><h3>Create Appointment</h3><p>Schedule an unplanned/manual service visit.</p></div><button type="button" class="icon-btn" (click)="closeCreate()">×</button></div>
 <div class="fields">
  <label>Vehicle<select [(ngModel)]="f.vehicleId"><option *ngFor="let v of vehicles" [value]="v.id">{{v.registrationNumber}} · {{v.model}}</option></select></label>
  <label>Source<select [(ngModel)]="f.sourceType"><option>Manual</option><option>Campaign</option></select></label>
  <label>Service Type<select [(ngModel)]="f.appointmentType"><option>Repair</option><option>Inspection</option><option>Campaign</option></select></label>
  <label>Priority<select [(ngModel)]="f.priority"><option>P1</option><option>P2</option><option>P3</option><option>P4</option></select></label>
  <label>Date / Time<input type="datetime-local" [(ngModel)]="f.startAt" (change)="loadCapacity()"></label>
  <label>Planned Hours<input type="number" min="0.5" step="0.5" [(ngModel)]="f.plannedHours"></label>
  <label>Service Centre<input [(ngModel)]="f.serviceCentre"></label>
  <label>Bay<select [(ngModel)]="f.bay"><option value="">Select bay</option><option *ngFor="let b of capacity?.bays" [value]="b.bayCode">{{b.bayCode}} · {{b.bayType}} · {{b.bookedHours}}h booked</option><option *ngIf="!capacity?.bays?.length">Bay-01</option><option *ngIf="!capacity?.bays?.length">Bay-02</option><option *ngIf="!capacity?.bays?.length">Bay-03</option></select></label>
  <label class="wide">Reason / Complaint<input [(ngModel)]="f.reason"></label>
 </div>
 <div class="actions"><button type="button" class="btn btn-outline" (click)="closeCreate()">Cancel</button><button type="button" class="btn btn-primary" [disabled]="saving" (click)="save()">{{saving?'Saving...':'Save Appointment'}}</button></div>
 <div class="error" *ngIf="message">{{message}}</div>
</div>

<div class="modal-card" *ngIf="checkOpen">
 <div class="modal-head"><div><h3>Check In & Assign Service</h3><p>{{checkAppointment?.vehicle}} · {{serviceLabel(checkAppointment)}}</p></div><button type="button" class="icon-btn" (click)="closeCheckin()">×</button></div>
 <div class="check-summary"><span>Scheduled <b><app-fleet-date [value]="checkAppointment?.startAt"></app-fleet-date></b></span><span>Bay <b>{{checkAppointment?.bay||'-'}}</b></span><span>Source <b>{{checkAppointment?.sourceType}}</b></span><span *ngIf="checkAppointment?.pmObligationId">PM <b>{{pmLabel(checkAppointment?.pmObligationId)}}</b></span></div>
 <div class="open-req" *ngIf="openRequestsForVehicle(checkAppointment?.vehicleId).length"><b>Open service requests for this vehicle:</b> {{openRequestsForVehicle(checkAppointment?.vehicleId).length}}. Select any requests below to include in this Job Card.</div>
 <div class="fields">
   <label>Assigned supervisor<app-supervisor-select [(value)]="assignedSupervisor"></app-supervisor-select></label>
  <label>Current Odometer (km)<input type="number" [(ngModel)]="check.odometerKm"></label>
   <label>Operating Hours<input type="number" [(ngModel)]="check.operatingHours"></label>
   <label>Energy Used (kWh)<input type="number" [(ngModel)]="check.energyKwh"></label>
   <label>Bay<input [value]="checkAppointment?.bay||'-'" disabled><small>Bay is reserved by the appointment. Technician/team is assigned later by the workshop supervisor.</small></label>
   <label class="wide">Additional Complaint<textarea [(ngModel)]="check.complaint" placeholder="Optional issue reported when the vehicle arrives"></textarea></label>
   <label class="wide">Arrival Condition / Remarks<textarea [(ngModel)]="check.remarks" placeholder="Optional arrival condition or service-advisor remarks"></textarea></label>
 </div>
 <div class="request-list"><label class="request-row" *ngFor="let r of candidateRequests"><input type="checkbox" [(ngModel)]="r._selected"><span>{{r.requestNumber}} · {{r.description}}</span></label></div><div class="actions"><button class="btn btn-outline" (click)="closeCheckin()">Cancel</button><button class="btn btn-primary" [disabled]="saving" (click)="confirmCheckin()">{{saving?'Checking in...':'Check In & Assign'}}</button></div>
 <div class="error" *ngIf="message">{{message}}</div>
</div>

<div class="modal-card" *ngIf="startOpen">
 <div class="modal-head"><div><h3>Create & Assign Service</h3><p>{{startAppointment?.vehicle}} · {{serviceLabel(startAppointment)}}</p></div><button class="icon-btn" (click)="closeStart()">×</button></div>
 <div class="source-box" *ngIf="startAppointment?.pmObligationId"><b>Scheduled PM</b><span>{{pmLabel(startAppointment?.pmObligationId)}}</span><small>The approved PM Task Matrix will generate automatically.</small></div>
 <label>Assigned supervisor<app-supervisor-select [(value)]="assignedSupervisor"></app-supervisor-select></label><div class="source-box"><b>Additional Work</b><span>Select any service requests to include in the same Work Order.</span></div>
 <div class="request-list" *ngIf="candidateRequests.length;else noRequests">
  <label class="request-row" *ngFor="let r of candidateRequests"><input type="checkbox" [(ngModel)]="r._selected"><span><b>{{r.requestNumber}}</b> · {{r.requestType}}<small>{{r.description}}</small></span></label>
 </div>
 <ng-template #noRequests><div class="empty">No additional maintenance requests selected for this visit.</div></ng-template>
 <div class="warning" *ngIf="startAppointment?.pmObligationId">PM Start is blocked if the selected PM level has no executable Task Matrix rows.</div>
 <div class="actions"><button class="btn btn-outline" (click)="closeStart()">Cancel</button><button class="btn btn-primary" [disabled]="saving" (click)="startSelected()">{{saving?'Starting...':'Start Work'}}</button></div>
 <div class="error" *ngIf="message">{{message}}</div>
</div>

<div class="card"><div class="fleet-grid-scroll" role="region" aria-label="Records" tabindex="0"><table fleetGrid><tr><th>No.</th><th>Vehicle</th><th>Source</th><th>Service</th><th>Start</th><th>Bay</th><th>Status</th><th>Action</th></tr>
<tr *ngFor="let x of rows"><td>{{x.appointmentNumber}}</td><td><b>{{x.vehicle}}</b></td><td>{{x.sourceType}}<small>{{sourceDetail(x)}}</small></td><td>{{serviceLabel(x)}}</td><td class="date-cell"><app-fleet-date [value]="x.startAt"></app-fleet-date></td><td>{{x.bay}}</td><td>{{x.status}}</td><td class="act">
<button *ngIf="x.status==='Requested'" class="btn btn-outline" (click)="setStatus(x,'Confirmed')">Confirm</button>
<button *ngIf="x.status==='Requested'||x.status==='Confirmed'" class="btn btn-primary" (click)="openCheckin(x)">Check In Vehicle</button>
<button *ngIf="x.status==='Checked-In'" class="btn btn-primary" (click)="prepareStart(x)">Start Work</button>
<button *ngIf="x.status==='Requested'||x.status==='Confirmed'" class="btn btn-outline" (click)="setStatus(x,'Cancelled')">Cancel</button>
<span *ngIf="x.status==='In Progress'">In workshop</span>
</td></tr>
<tr *ngIf="!rows.length"><td colspan="8" class="empty">No appointments yet.</td></tr></table></div></div>
</section>`,styles:[`.page{padding:26px}.title{display:flex;justify-content:space-between;align-items:center}.title p{color:#64748b}.info{background:#eef6ff;border:1px solid #bfdbfe;padding:10px 12px;border-radius:8px;margin:12px 0}.card{margin-top:14px}.capacity{display:flex;gap:22px}.capacity div{display:flex;flex-direction:column}.capacity span{color:#64748b;font-size:12px}.modal-backdrop{position:fixed;inset:0;background:rgba(15,23,42,.35);z-index:1000}.modal-card{position:fixed;z-index:1001;left:50%;top:50%;transform:translate(-50%,-50%);width:min(880px,94vw);max-height:90vh;overflow:auto;background:#fff;border-radius:12px;padding:20px;box-shadow:0 20px 60px rgba(0,0,0,.2)}.modal-head{display:flex;justify-content:space-between}.modal-head h3{margin:0}.modal-head p{margin:4px 0;color:#64748b}.icon-btn{border:0;background:transparent;font-size:28px}.fields{display:grid;grid-template-columns:repeat(2,1fr);gap:12px;margin:18px 0}.wide{grid-column:1/-1}label{font-size:12px}label small{display:block;color:#64748b;margin-top:4px}input,select,textarea{display:block;width:100%;box-sizing:border-box;padding:9px;margin-top:4px}textarea{min-height:64px}.actions{display:flex;justify-content:flex-end;gap:8px}.error{color:#b91c1c;margin-top:8px}.act{display:flex;gap:5px;align-items:center;flex-wrap:wrap}.empty{text-align:center;color:#64748b;padding:14px}.check-summary{display:flex;gap:8px;margin:14px 0;flex-wrap:wrap}.check-summary span,.source-box{background:#f8fafc;border:1px solid #e2e8f0;border-radius:8px;padding:8px 10px;font-size:12px}.check-summary b{margin-left:4px}.open-req{background:#fff7ed;border:1px solid #fed7aa;border-radius:8px;padding:9px 10px;font-size:12px}.source-box{display:flex;gap:10px;align-items:center;margin:8px 0}.source-box small{margin-left:auto;color:#64748b}.request-list{border:1px solid #e2e8f0;border-radius:8px;margin:12px 0}.request-row{display:flex;gap:10px;padding:10px;border-bottom:1px solid #e5e7eb;align-items:flex-start}.request-row:last-child{border-bottom:0}.request-row input{width:auto;margin-top:3px}.request-row span{flex:1}.request-row small{display:block}.warning{background:#fef3c7;border:1px solid #f59e0b;padding:9px;border-radius:8px;font-size:12px;margin:12px 0}td small{display:block;color:#64748b;margin-top:3px}`]})
export class AppointmentsComponent implements OnInit{
 rows:any[]=[];vehicles:any[]=[];pmRows:any[]=[];requests:any[]=[];capacity:any;capacityDate='';createOpen=false;checkOpen=false;startOpen=false;saving=false;message='';checkAppointment:any=null;startAppointment:any=null;candidateRequests:any[]=[];check:any={};assignedSupervisor='';
 f:any={vehicleId:'',pmObligationId:null,sourceType:'Manual',sourceReference:'',startAt:'',serviceCentre:'',bay:'',technicianId:null,appointmentType:'Repair',priority:'P3',reason:'',plannedHours:2,createdBy:'Service Manager'};
 constructor(private h:HttpClient,private router:Router){}
 ngOnInit(){this.h.get<any[]>('/api/vehicles').subscribe(x=>{this.vehicles=x;if(x.length)this.f.vehicleId=x[0].id});this.load();}
 reset(){const v=this.vehicles[0];this.f={vehicleId:v?.id||'',pmObligationId:null,sourceType:'Manual',sourceReference:'',startAt:'',serviceCentre:v?.serviceCentreCode||'',bay:'',technicianId:null,appointmentType:'Repair',priority:'P3',reason:'',plannedHours:2,createdBy:'Service Manager'};this.capacity=null;this.capacityDate='';}
 openCreate(){this.message='';this.reset();this.createOpen=true}closeCreate(){if(!this.saving)this.createOpen=false}
 load(){this.h.get<any[]>('/api/appointments').subscribe(x=>this.rows=x);this.h.get<any[]>('/api/pm/due-board').subscribe(x=>this.pmRows=x);this.h.get<any[]>('/api/maintenance-requests').subscribe(x=>this.requests=x)}
 loadCapacity(){if(!this.f.startAt)return;this.capacityDate=this.f.startAt.substring(0,10);this.h.get(`/api/capacity?date=${this.capacityDate}`).subscribe(x=>{this.capacity=x;const bays=this.capacity?.bays||[];if(bays.length&&!this.f.bay)this.f.bay=bays[0].bayCode})}
 save(){if(!this.f.vehicleId||!this.f.startAt){this.message='Vehicle and date/time are required.';return}if(!this.f.bay){this.message='Select a service bay.';return}this.saving=true;this.message='';this.h.post('/api/appointments',{...this.f,startAt:new Date(this.f.startAt).toISOString(),plannedHours:Number(this.f.plannedHours),technicianId:null}).subscribe({next:()=>{this.saving=false;this.createOpen=false;this.reset();this.load()},error:e=>{this.saving=false;this.message=e.error?.message||e.error?.detail||'Unable to save appointment'}})}
 setStatus(x:any,status:string){this.h.put(`/api/appointments/${x.id}/status`,{status}).subscribe(()=>this.load())}
 pmRow(id:string){return this.pmRows.find(p=>p.id===id)}
 pmLabel(id:string){const p=this.pmRow(id);return p?`${p.plan} · ${p.planName}`:'PM obligation'}
 serviceLabel(x:any){return x?.pmObligationId?`Preventive Maintenance · ${this.pmLabel(x.pmObligationId)}`:x?.appointmentType||''}
 sourceDetail(x:any){if(x?.pmObligationId)return this.pmLabel(x.pmObligationId);const r=this.requests.find(m=>m.id===x?.sourceReference);return r?.requestNumber||this.shortRef(x?.sourceReference||'')}
 shortRef(x:string){return x.length>12?x.substring(0,8)+'…':x}
 openRequestsForVehicle(vehicleId:string){return this.requests.filter(r=>r.vehicleId===vehicleId&&['Open','Scheduled','Vehicle Arrived'].includes(r.status))}
 openCheckin(x:any){this.assignedSupervisor='';this.candidateRequests=this.openRequestsForVehicle(x.vehicleId).filter(r=>!r.jobCardId).map(r=>({...r,_selected:x.sourceType==='Maintenance Request'&&r.id===x.sourceReference}));const v=this.vehicles.find(z=>z.id===x.vehicleId);this.checkAppointment=x;this.check={odometerKm:Number(v?.odometerKm||0),operatingHours:Number(v?.operatingHours||0),energyKwh:Number(v?.energyKwh||0),complaint:'',remarks:''};this.message='';this.checkOpen=true}
 closeCheckin(){if(!this.saving){this.checkOpen=false;this.checkAppointment=null}}
 confirmCheckin(){this.startAppointment=this.checkAppointment;this.startSelected()}
 prepareStart(x:any){if(x.status!=='Checked-In'){alert('Check in the vehicle before starting work.');return}this.message='';if(x.pmObligationId){this.validatePm(x)}else this.openStart(x)}
 validatePm(x:any){const ob=this.pmRow(x.pmObligationId);const v=this.vehicles.find(z=>z.id===x.vehicleId);if(!ob||!v?.maintenanceProgramId){this.message='PM configuration is incomplete. PM obligation or assigned PM program could not be resolved.';return}this.h.get<any>(`/api/pm/programs/${v.maintenanceProgramId}/service-matrix`).subscribe({next:m=>{const level=(m.levels||[]).find((p:any)=>p.planCode===ob.plan);const count=level?(m.assignments||[]).filter((a:any)=>a.maintenancePlanId===level.id&&String(a.actionCode||'').trim()!=='').length:0;if(!level||count===0){this.message=`Cannot start ${ob.plan}. No executable Task Matrix rows are configured for this PM level.`;return}this.openStart(x)},error:()=>this.message='Unable to validate the PM Task Matrix. Start Work is blocked.'})}
 openStart(x:any){this.assignedSupervisor='';this.startAppointment=x;const related=this.openRequestsForVehicle(x.vehicleId).filter(r=>r.jobCardId==null);this.candidateRequests=related.map(r=>({...r,_selected:(r.sourceType==='Check-In'&&r.sourceReference===x.id)||(x.sourceType==='Maintenance Request'&&r.id===x.sourceReference)}));this.startOpen=true}
 closeStart(){if(!this.saving){this.startOpen=false;this.startAppointment=null;this.candidateRequests=[]}}
 startSelected(){const x=this.startAppointment;if(!x||this.saving)return;if(!this.assignedSupervisor.trim()){this.message='Enter the assigned supervisor.';return}this.saving=true;this.message='';const body={assignedSupervisor:this.assignedSupervisor.trim(),requestIds:this.candidateRequests.filter(m=>m._selected).map(m=>m.id),...(this.checkOpen?this.check:{})};this.h.post<any>(`/api/appointments/${x.id}/start-service`,body).subscribe({next:r=>this.finishStart(r.jobCard.id),error:e=>{this.saving=false;this.message=e.error?.message||'Unable to create assigned service. Please retry.'}})}
 finishStart(jobId:string,failed=0){this.saving=false;this.startOpen=false;if(failed)alert(`${failed} additional request(s) could not be added. Review the Work Order.`);this.router.navigate(['/service-workspace',jobId])}
}
