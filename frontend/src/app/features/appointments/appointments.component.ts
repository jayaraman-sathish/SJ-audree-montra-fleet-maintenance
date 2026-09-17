import {Component,OnInit} from '@angular/core';
import {CommonModule} from '@angular/common';
import {FormsModule} from '@angular/forms';
import {HttpClient} from '@angular/common/http';

@Component({selector:'app-appointments',standalone:true,imports:[CommonModule,FormsModule],template:`
<section class="page">
<div class="title"><div><h2>Appointments & Capacity</h2><p>Appointment = when and where the vehicle will visit. PM appointments originate from PM Obligations.</p></div><button type="button" class="btn btn-primary" (click)="openCreate()">+ Appointment</button></div>
<div class="info"><b>Planned PM:</b> use <b>PM Obligations → Schedule Appointment</b>. Use + Appointment here for repair, inspection or campaign visits.</div>
<div class="capacity card"><div><b>Selected day capacity</b><span>{{capacityDate||'Select appointment date'}}</span></div><div *ngFor="let b of capacity?.bays"><b>{{b.bayCode}}</b><span>{{b.bookedHours}} h booked</span></div></div>

<div class="modal-backdrop" *ngIf="createOpen||checkOpen"></div>
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
 <div class="modal-head"><div><h3>Check In Vehicle</h3><p>{{checkAppointment?.vehicle}} · {{serviceLabel(checkAppointment)}}</p></div><button type="button" class="icon-btn" (click)="closeCheckin()">×</button></div>
 <div class="check-summary"><span>Scheduled <b>{{checkAppointment?.startAt|date:'medium'}}</b></span><span>Bay <b>{{checkAppointment?.bay||'-'}}</b></span><span>Source <b>{{checkAppointment?.sourceType}}</b></span></div>
 <div class="fields">
   <label>Current Odometer (km)<input type="number" [(ngModel)]="check.odometerKm"></label>
   <label>Operating Hours<input type="number" [(ngModel)]="check.operatingHours"></label>
   <label>Energy Used (kWh)<input type="number" [(ngModel)]="check.energyKwh"></label>
   <label>Bay<input [value]="checkAppointment?.bay||'-'" disabled><small>Bay is reserved by the appointment. Technician/team is assigned later by the workshop supervisor.</small></label>
   <label class="wide">Additional Complaint<textarea [(ngModel)]="check.complaint" placeholder="Optional issue reported when the vehicle arrives"></textarea></label>
   <label class="wide">Arrival Condition / Remarks<textarea [(ngModel)]="check.remarks" placeholder="Optional arrival condition or service-advisor remarks"></textarea></label>
 </div>
 <div class="actions"><button class="btn btn-outline" (click)="closeCheckin()">Cancel</button><button class="btn btn-primary" [disabled]="saving" (click)="confirmCheckin()">{{saving?'Checking in...':'Check In Vehicle'}}</button></div>
 <div class="error" *ngIf="message">{{message}}</div>
</div>

<div class="card"><table><tr><th>No.</th><th>Vehicle</th><th>Source</th><th>Service</th><th>Start</th><th>Bay</th><th>Status</th><th>Action</th></tr>
<tr *ngFor="let x of rows"><td>{{x.appointmentNumber}}</td><td><b>{{x.vehicle}}</b></td><td>{{x.sourceType}}<small>{{x.sourceReference?shortRef(x.sourceReference):''}}</small></td><td>{{serviceLabel(x)}}</td><td>{{x.startAt|date:'medium'}}</td><td>{{x.bay}}</td><td>{{x.status}}</td><td class="act">
<button *ngIf="x.status==='Requested'" class="btn btn-outline" (click)="setStatus(x,'Confirmed')">Confirm</button>
<button *ngIf="x.status==='Requested'||x.status==='Confirmed'" class="btn btn-primary" (click)="openCheckin(x)">Check In Vehicle</button>
<button *ngIf="x.status==='Checked-In'" class="btn btn-primary" (click)="start(x)">Start Work</button>
<button *ngIf="x.status==='Requested'||x.status==='Confirmed'" class="btn btn-outline" (click)="setStatus(x,'Cancelled')">Cancel</button>
<span *ngIf="x.status==='In Progress'">In workshop</span>
</td></tr>
<tr *ngIf="!rows.length"><td colspan="8" class="empty">No appointments yet.</td></tr></table></div>
</section>`,styles:[`.page{padding:26px}.title{display:flex;justify-content:space-between;align-items:center}.title p{color:#64748b}.info{background:#eef6ff;border:1px solid #bfdbfe;padding:10px 12px;border-radius:8px;margin:12px 0}.card{margin-top:14px}.capacity{display:flex;gap:22px}.capacity div{display:flex;flex-direction:column}.capacity span{color:#64748b;font-size:12px}.modal-backdrop{position:fixed;inset:0;background:rgba(15,23,42,.35);z-index:1000}.modal-card{position:fixed;z-index:1001;left:50%;top:50%;transform:translate(-50%,-50%);width:min(880px,94vw);max-height:90vh;overflow:auto;background:#fff;border-radius:12px;padding:20px;box-shadow:0 20px 60px rgba(0,0,0,.2)}.modal-head{display:flex;justify-content:space-between}.modal-head h3{margin:0}.modal-head p{margin:4px 0;color:#64748b}.icon-btn{border:0;background:transparent;font-size:28px}.fields{display:grid;grid-template-columns:repeat(2,1fr);gap:12px;margin:18px 0}.wide{grid-column:1/-1}label{font-size:12px}label small{display:block;color:#64748b;margin-top:4px}input,select,textarea{display:block;width:100%;box-sizing:border-box;padding:9px;margin-top:4px}textarea{min-height:64px}.actions{display:flex;justify-content:flex-end;gap:8px}.error{color:#b91c1c;margin-top:8px}.act{display:flex;gap:5px;align-items:center;flex-wrap:wrap}.empty{text-align:center;color:#64748b}.check-summary{display:flex;gap:8px;margin:14px 0}.check-summary span{background:#f8fafc;border:1px solid #e2e8f0;border-radius:8px;padding:8px 10px;font-size:12px}.check-summary b{margin-left:4px}td small{display:block;color:#64748b;margin-top:3px}`]})
export class AppointmentsComponent implements OnInit{
 rows:any[]=[];vehicles:any[]=[];capacity:any;capacityDate='';createOpen=false;checkOpen=false;saving=false;message='';checkAppointment:any=null;check:any={};
 f:any={vehicleId:'',pmObligationId:null,sourceType:'Manual',sourceReference:'',startAt:'',serviceCentre:'',bay:'',technicianId:null,appointmentType:'Repair',priority:'P3',reason:'',plannedHours:2,createdBy:'Service Manager'};
 constructor(private h:HttpClient){}
 ngOnInit(){this.h.get<any[]>('/api/vehicles').subscribe(x=>{this.vehicles=x;if(x.length)this.f.vehicleId=x[0].id});this.load();}
 reset(){const v=this.vehicles[0];this.f={vehicleId:v?.id||'',pmObligationId:null,sourceType:'Manual',sourceReference:'',startAt:'',serviceCentre:v?.serviceCentreCode||'',bay:'',technicianId:null,appointmentType:'Repair',priority:'P3',reason:'',plannedHours:2,createdBy:'Service Manager'};this.capacity=null;this.capacityDate='';}
 openCreate(){this.message='';this.reset();this.createOpen=true}closeCreate(){if(!this.saving)this.createOpen=false}
 load(){this.h.get<any[]>('/api/appointments').subscribe(x=>this.rows=x)}
 loadCapacity(){if(!this.f.startAt)return;this.capacityDate=this.f.startAt.substring(0,10);this.h.get(`/api/capacity?date=${this.capacityDate}`).subscribe(x=>{this.capacity=x;const bays=this.capacity?.bays||[];if(bays.length&&!this.f.bay)this.f.bay=bays[0].bayCode})}
 save(){if(!this.f.vehicleId||!this.f.startAt){this.message='Vehicle and date/time are required.';return}if(!this.f.bay){this.message='Select a service bay.';return}this.saving=true;this.message='';this.h.post('/api/appointments',{...this.f,startAt:new Date(this.f.startAt).toISOString(),plannedHours:Number(this.f.plannedHours),technicianId:null}).subscribe({next:()=>{this.saving=false;this.createOpen=false;this.reset();this.load()},error:e=>{this.saving=false;this.message=e.error?.message||e.error?.detail||'Unable to save appointment'}})}
 setStatus(x:any,status:string){this.h.put(`/api/appointments/${x.id}/status`,{status}).subscribe(()=>this.load())}
 serviceLabel(x:any){return x?.pmObligationId?`${x.appointmentType} · PM obligation`:x?.appointmentType||''}
 shortRef(x:string){return x.length>12?x.substring(0,8)+'…':x}
 openCheckin(x:any){const v=this.vehicles.find(z=>z.id===x.vehicleId);this.checkAppointment=x;this.check={odometerKm:Number(v?.odometerKm||0),operatingHours:Number(v?.operatingHours||0),energyKwh:Number(v?.energyKwh||0),complaint:'',remarks:''};this.message='';this.checkOpen=true}
 closeCheckin(){if(!this.saving){this.checkOpen=false;this.checkAppointment=null}}
 confirmCheckin(){const a=this.checkAppointment;const v=this.vehicles.find(z=>z.id===a?.vehicleId);if(!a||!v)return;this.saving=true;this.message='';const updated={...v,odometerKm:Number(this.check.odometerKm||0),operatingHours:Number(this.check.operatingHours||0),energyKwh:Number(this.check.energyKwh||0)};this.h.put(`/api/vehicles/${v.id}`,updated).subscribe({next:()=>{const description=[this.check.complaint,this.check.remarks].filter(Boolean).join(' · ');const afterRequest=()=>this.h.put(`/api/appointments/${a.id}/status`,{status:'Checked-In'}).subscribe({next:()=>{this.saving=false;this.checkOpen=false;this.load();this.h.get<any[]>('/api/vehicles').subscribe(x=>this.vehicles=x)},error:e=>{this.saving=false;this.message=e.error?.message||'Unable to check in'}});if(description){this.h.post('/api/maintenance-requests',{vehicleId:a.vehicleId,sourceType:'Check-In',sourceReference:a.id,requestType:'Repair',priority:a.priority||'P3',description,requestedBy:'Service Advisor',targetDate:null}).subscribe({next:afterRequest,error:afterRequest})}else afterRequest()},error:e=>{this.saving=false;this.message=e.error?.message||'Unable to update arrival readings'}})}
 start(x:any){if(x.status!=='Checked-In'){alert('Check in the vehicle before starting work.');return}this.h.post<any>(`/api/appointments/${x.id}/start-service`,{}).subscribe({next:r=>{const jobId=r?.jobCard?.id;if(!jobId){this.load();return}this.h.get<any[]>('/api/maintenance-requests').subscribe(reqs=>{const related=reqs.filter(m=>m.status==='Open'&&((m.sourceType==='Check-In'&&m.sourceReference===x.id)||(x.sourceType==='Maintenance Request'&&m.id===x.sourceReference)));if(!related.length){this.load();return}let pending=related.length;for(const m of related){this.h.post(`/api/job-cards/${jobId}/work-items`,{workType:'Repair',description:m.description,standardRepairHours:null,requiresQc:true,requiresHvAuthorization:false}).subscribe({next:()=>this.h.put(`/api/maintenance-requests/${m.id}/status`,{status:'Converted'}).subscribe(()=>{if(--pending===0)this.load()}),error:()=>{if(--pending===0)this.load()}})}})},error:e=>alert(e.error?.message||'Unable to start work')})}
}
