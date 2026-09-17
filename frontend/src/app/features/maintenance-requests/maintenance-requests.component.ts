import {Component,OnInit} from '@angular/core';
import {CommonModule} from '@angular/common';
import {FormsModule} from '@angular/forms';
import {HttpClient} from '@angular/common/http';

@Component({selector:'app-maintenance-requests',standalone:true,imports:[CommonModule,FormsModule],template:`
<section class="page">
<div class="title"><div><h2>Maintenance Requests</h2><p>Unplanned or manually reported service needs. Planned preventive maintenance is generated under PM Obligations.</p></div><button class="btn btn-primary" (click)="open()">+ Maintenance Request</button></div>
<div class="note"><b>Request = why service is needed.</b> Appointment = when/where. Check-In = vehicle arrived. Work Order / Job Card = workshop execution.</div>

<div class="modal-backdrop" *ngIf="show||planOpen"></div>
<div class="modal-card" *ngIf="show"><div class="modal-head"><div><h3>Create Maintenance Request</h3><p>Use for repair complaints, inspections and other unplanned/manual work.</p></div><button class="icon-btn" (click)="show=false">×</button></div>
<div class="fields"><label>Vehicle<select [(ngModel)]="f.vehicleId"><option *ngFor="let v of vehicles" [value]="v.id">{{v.registrationNumber}} · {{v.model}}</option></select></label>
<label>Source<select [(ngModel)]="f.sourceType"><option>Manual</option><option>Customer</option><option>Inspection</option><option>Campaign</option></select></label>
<label>Type<select [(ngModel)]="f.requestType"><option>Repair</option><option>Inspection</option><option>Campaign</option></select></label>
<label>Priority<select [(ngModel)]="f.priority"><option>P1</option><option>P2</option><option>P3</option><option>P4</option></select></label>
<label class="wide">Complaint / Description<textarea [(ngModel)]="f.description"></textarea></label><label>Target Date<input type="date" [(ngModel)]="target"></label></div>
<div class="actions"><button class="btn btn-outline" (click)="show=false">Cancel</button><button class="btn btn-primary" (click)="save()">Save Request</button></div><div class="error">{{message}}</div></div>

<div class="modal-card" *ngIf="planOpen"><div class="modal-head"><div><h3>{{checkInNow?'Check In Now':'Schedule Appointment'}}</h3><p>{{selected?.requestNumber}} · {{selected?.vehicle}} · {{selected?.description}}</p></div><button class="icon-btn" (click)="closePlan()">×</button></div>
<div class="fields">
<label *ngIf="!checkInNow">Date / Time<input type="datetime-local" [(ngModel)]="appointment.startAt" (change)="loadCapacity()"></label>
<label>Planned Hours<input type="number" min="0.5" step="0.5" [(ngModel)]="appointment.plannedHours"></label>
<label>Service Centre<input [(ngModel)]="appointment.serviceCentre"></label>
<label>Bay<select [(ngModel)]="appointment.bay"><option value="">Select bay</option><option *ngFor="let b of capacity?.bays" [value]="b.bayCode">{{b.bayCode}} · {{b.bayType}} · {{b.bookedHours}}h booked</option><option *ngIf="!capacity?.bays?.length">Bay-01</option><option *ngIf="!capacity?.bays?.length">Bay-02</option><option *ngIf="!capacity?.bays?.length">Bay-03</option></select></label>
<label class="wide">Reason<input [(ngModel)]="appointment.reason"></label>
</div><div class="actions"><button class="btn btn-outline" (click)="closePlan()">Cancel</button><button class="btn btn-primary" [disabled]="saving" (click)="saveAppointment()">{{saving?'Saving...':(checkInNow?'Create & Check In':'Schedule Appointment')}}</button></div><div class="error">{{message}}</div></div>

<div class="card"><table><tr><th>Request</th><th>Vehicle</th><th>Source</th><th>Type</th><th>Priority</th><th>Description</th><th>Status</th><th>Action</th></tr>
<tr *ngFor="let x of rows"><td>{{x.requestNumber}}</td><td>{{x.vehicle}}</td><td>{{x.sourceType}}</td><td>{{x.requestType}}</td><td>{{x.priority}}</td><td>{{x.description}}</td><td>{{x.status}}</td><td class="act"><button *ngIf="x.status==='Open'" class="btn btn-primary" (click)="schedule(x,false)">Schedule Appointment</button><button *ngIf="x.status==='Open'" class="btn btn-outline" (click)="schedule(x,true)">Check In Now</button><button *ngIf="x.status==='Open'" class="btn btn-outline" (click)="setStatus(x,'Cancelled')">Cancel</button><span *ngIf="x.status!=='Open'">{{x.status}}</span></td></tr>
<tr *ngIf="!rows.length"><td colspan="8" class="empty">No maintenance requests.</td></tr></table></div>
</section>`,styles:[`.page{padding:26px}.title{display:flex;justify-content:space-between;align-items:center}.title p{color:#64748b}.note{background:#eef6ff;border:1px solid #bfdbfe;border-radius:8px;padding:10px 12px;margin:12px 0}.card{margin-top:14px}.modal-backdrop{position:fixed;inset:0;background:rgba(15,23,42,.35);z-index:1000}.modal-card{position:fixed;z-index:1001;left:50%;top:50%;transform:translate(-50%,-50%);width:min(820px,94vw);background:#fff;border-radius:12px;padding:20px;box-shadow:0 20px 60px rgba(0,0,0,.2)}.modal-head{display:flex;justify-content:space-between}.modal-head h3{margin:0}.modal-head p{margin:4px 0;color:#64748b}.icon-btn{border:0;background:transparent;font-size:28px}.fields{display:grid;grid-template-columns:repeat(2,1fr);gap:12px;margin:16px 0}.wide{grid-column:1/-1}label{font-size:12px}input,select,textarea{display:block;width:100%;box-sizing:border-box;padding:9px;margin-top:4px}textarea{min-height:60px}.actions{display:flex;justify-content:flex-end;gap:8px}.error{color:#b91c1c}.empty{text-align:center;color:#64748b;padding:18px}.act{display:flex;gap:5px;flex-wrap:wrap}.btn{white-space:nowrap}`]})
export class MaintenanceRequestsComponent implements OnInit{
 rows:any[]=[];vehicles:any[]=[];show=false;planOpen=false;message='';target='';selected:any=null;checkInNow=false;saving=false;capacity:any;
 f:any={vehicleId:'',sourceType:'Manual',sourceReference:'',requestType:'Repair',priority:'P3',description:'',requestedBy:'Fleet User',targetDate:null};
 appointment:any={startAt:'',serviceCentre:'',bay:'',plannedHours:2,reason:''};
 constructor(private h:HttpClient){}
 ngOnInit(){this.h.get<any[]>('/api/vehicles').subscribe(x=>{this.vehicles=x;if(x.length)this.f.vehicleId=x[0].id});this.load()}
 load(){this.h.get<any[]>('/api/maintenance-requests').subscribe(x=>this.rows=x)}
 open(){this.message='';this.f={vehicleId:this.vehicles[0]?.id||'',sourceType:'Manual',sourceReference:'',requestType:'Repair',priority:'P3',description:'',requestedBy:'Fleet User',targetDate:null};this.target='';this.show=true}
 save(){if(!this.f.vehicleId||!this.f.description){this.message='Vehicle and description are required.';return}this.f.targetDate=this.target?new Date(this.target).toISOString():null;this.h.post('/api/maintenance-requests',this.f).subscribe({next:()=>{this.show=false;this.load()},error:e=>this.message=e.error?.message||'Unable to save'})}
 vehicle(id:string){return this.vehicles.find(v=>v.id===id)}
 schedule(x:any,now:boolean){this.selected=x;this.checkInNow=now;const v=this.vehicle(x.vehicleId);this.appointment={startAt:now?new Date().toISOString().slice(0,16):'',serviceCentre:v?.serviceCentreCode||'',bay:'',plannedHours:2,reason:x.description};this.capacity=null;this.message='';this.planOpen=true;if(now)this.loadCapacity()}
 closePlan(){if(!this.saving)this.planOpen=false}
 loadCapacity(){const start=this.appointment.startAt;if(!start)return;const d=start.substring(0,10);this.h.get(`/api/capacity?date=${d}`).subscribe(x=>{this.capacity=x;const bays=this.capacity?.bays||[];if(bays.length&&!this.appointment.bay)this.appointment.bay=bays[0].bayCode})}
 saveAppointment(){if(!this.selected)return;if(!this.appointment.startAt){this.message='Appointment date/time is required.';return}if(!this.appointment.bay){this.message='Select a service bay.';return}this.saving=true;this.message='';const body={vehicleId:this.selected.vehicleId,pmObligationId:null,sourceType:'Maintenance Request',sourceReference:this.selected.id,startAt:new Date(this.appointment.startAt).toISOString(),serviceCentre:this.appointment.serviceCentre||this.vehicle(this.selected.vehicleId)?.serviceCentreCode||'Service Centre',bay:this.appointment.bay,technicianId:null,appointmentType:this.selected.requestType,priority:this.selected.priority,reason:this.appointment.reason,plannedHours:Number(this.appointment.plannedHours)||2,createdBy:'Service Advisor'};this.h.post<any>('/api/appointments',body).subscribe({next:a=>{const finish=()=>this.h.put(`/api/maintenance-requests/${this.selected.id}/status`,{status:this.checkInNow?'Vehicle Arrived':'Scheduled'}).subscribe({next:()=>{this.saving=false;this.planOpen=false;this.load()},error:()=>{this.saving=false;this.planOpen=false;this.load()}});if(this.checkInNow)this.h.put(`/api/appointments/${a.id}/status`,{status:'Checked-In'}).subscribe({next:finish,error:finish});else finish()},error:e=>{this.saving=false;this.message=e.error?.message||'Unable to create appointment'}})}
 setStatus(x:any,status:string){this.h.put(`/api/maintenance-requests/${x.id}/status`,{status}).subscribe(()=>this.load())}
}
