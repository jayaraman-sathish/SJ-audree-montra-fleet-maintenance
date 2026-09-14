import {Component,OnInit} from '@angular/core';
import {CommonModule} from '@angular/common';
import {FormsModule} from '@angular/forms';
import {HttpClient} from '@angular/common/http';

@Component({selector:'app-appointments',standalone:true,imports:[CommonModule,FormsModule],template:`
<section class="page">
<div class="title"><div><h2>Appointments & Capacity</h2><p>Multiple appointments with bay / technician capacity control and service lifecycle.</p></div><button type="button" class="btn btn-primary" (click)="openCreate()">+ Appointment</button></div>
<div class="capacity card"><div><b>Selected day capacity</b><span>{{capacityDate||'Select appointment date'}}</span></div><div *ngFor="let b of capacity?.bays"><b>{{b.bayCode}}</b><span>{{b.bookedHours}} h booked</span></div></div>
<div class="modal-backdrop" *ngIf="createOpen" (click)="closeCreate()"></div>
<div class="modal-card" *ngIf="createOpen">
 <div class="modal-head"><h3>Create Appointment</h3><button type="button" class="icon-btn" (click)="closeCreate()">×</button></div>
 <div class="fields">
  <label>Vehicle<select [(ngModel)]="f.vehicleId"><option *ngFor="let v of vehicles" [value]="v.id">{{v.registrationNumber}} · {{v.model}}</option></select></label>
  <label>Source<select [(ngModel)]="f.sourceType"><option>PM</option><option>Breakdown</option><option>Campaign</option><option>Manual</option></select></label>
  <label>Service Type<select [(ngModel)]="f.appointmentType"><option>PM</option><option>Repair</option><option>Inspection</option><option>Campaign</option></select></label>
  <label>Priority<select [(ngModel)]="f.priority"><option>P1</option><option>P2</option><option>P3</option><option>P4</option></select></label>
  <label>Date / Time<input type="datetime-local" [(ngModel)]="f.startAt" (change)="loadCapacity()"></label>
  <label>Planned Hours<input type="number" min="0.5" step="0.5" [(ngModel)]="f.plannedHours"></label>
  <label>Bay<select [(ngModel)]="f.bay"><option *ngFor="let b of capacity?.bays" [value]="b.bayCode">{{b.bayCode}} · {{b.bayType}} · {{b.bookedHours}}h booked</option><option *ngIf="!capacity">Bay-01</option></select></label>
  <label>Technician<select [(ngModel)]="f.technicianId"><option [ngValue]="null">Unassigned</option><option *ngFor="let t of technicians" [ngValue]="t.id">{{t.name}} · {{t.skillCodes}}{{t.hvAuthorized?' · HV':''}}</option></select></label>
  <label class="wide">Reason / Complaint<input [(ngModel)]="f.reason"></label>
 </div>
 <div class="actions"><button type="button" class="btn btn-outline" (click)="closeCreate()">Cancel</button><button type="button" class="btn btn-primary" [disabled]="saving" (click)="save()">{{saving?'Saving...':'Save Appointment'}}</button></div>
 <div class="error" *ngIf="message">{{message}}</div>
</div>

<div class="card"><table><tr><th>No.</th><th>Vehicle</th><th>Source</th><th>Start</th><th>Bay</th><th>Technician</th><th>Type</th><th>Status</th><th>Action</th></tr>
<tr *ngFor="let x of rows"><td>{{x.appointmentNumber}}</td><td>{{x.vehicle}}</td><td>{{x.sourceType}}</td><td>{{x.startAt|date:'medium'}}</td><td>{{x.bay}}</td><td>{{x.technician||'Unassigned'}}</td><td>{{x.appointmentType}}</td><td>{{x.status}}</td><td class="act">
<button *ngIf="x.status==='Requested'" class="btn btn-outline" (click)="setStatus(x,'Confirmed')">Confirm</button>
<button *ngIf="x.status==='Confirmed'" class="btn btn-outline" (click)="setStatus(x,'Checked-In')">Check-in</button>
<button *ngIf="x.status==='Checked-In'||x.status==='Confirmed'||x.status==='Requested'" class="btn btn-primary" (click)="start(x)">Start Service</button>
<button *ngIf="x.status==='Requested'||x.status==='Confirmed'" class="btn btn-outline" (click)="setStatus(x,'Cancelled')">Cancel</button></td></tr>
<tr *ngIf="!rows.length"><td colspan="9" class="empty">No appointments yet.</td></tr></table></div>
</section>`,styles:[`.page{padding:26px}.title{display:flex;justify-content:space-between;align-items:center}.title p{color:#64748b}.card{margin-top:14px}.capacity{display:flex;gap:22px}.capacity div{display:flex;flex-direction:column}.capacity span{color:#64748b;font-size:12px}.modal-backdrop{position:fixed;inset:0;background:rgba(15,23,42,.35);z-index:1000}.modal-card{position:fixed;z-index:1001;left:50%;top:50%;transform:translate(-50%,-50%);width:min(880px,94vw);background:#fff;border-radius:12px;padding:20px;box-shadow:0 20px 60px rgba(0,0,0,.2)}.modal-head{display:flex;justify-content:space-between}.icon-btn{border:0;background:transparent;font-size:28px}.fields{display:grid;grid-template-columns:repeat(2,1fr);gap:12px;margin:18px 0}.wide{grid-column:1/-1}label{font-size:12px}input,select{display:block;width:100%;padding:9px;margin-top:4px}.actions{display:flex;justify-content:flex-end;gap:8px}.error{color:#b91c1c;margin-top:8px}.act{display:flex;gap:5px}.empty{text-align:center;color:#64748b}`]})
export class AppointmentsComponent implements OnInit{
 rows:any[]=[];vehicles:any[]=[];technicians:any[]=[];capacity:any;capacityDate='';createOpen=false;saving=false;message='';
 f:any={vehicleId:'',pmObligationId:null,sourceType:'Manual',sourceReference:'',startAt:'',serviceCentre:'Chennai Service Centre',bay:'Bay-01',technicianId:null,appointmentType:'PM',priority:'P3',reason:'',plannedHours:2,createdBy:'Service Manager'};
 constructor(private h:HttpClient){}
 ngOnInit(){this.h.get<any[]>('/api/vehicles').subscribe(x=>{this.vehicles=x;if(x.length)this.f.vehicleId=x[0].id});this.h.get<any[]>('/api/technicians').subscribe(x=>this.technicians=x);this.load();}
 reset(){this.f={vehicleId:this.vehicles[0]?.id||'',pmObligationId:null,sourceType:'Manual',sourceReference:'',startAt:'',serviceCentre:'Chennai Service Centre',bay:'Bay-01',technicianId:null,appointmentType:'PM',priority:'P3',reason:'',plannedHours:2,createdBy:'Service Manager'};this.capacity=null;this.capacityDate='';}
 openCreate(){this.message='';this.reset();this.createOpen=true}closeCreate(){if(!this.saving)this.createOpen=false}
 load(){this.h.get<any[]>('/api/appointments').subscribe(x=>this.rows=x)}
 loadCapacity(){if(!this.f.startAt)return;this.capacityDate=this.f.startAt.substring(0,10);this.h.get(`/api/capacity?date=${this.capacityDate}`).subscribe(x=>{this.capacity=x; if(this.capacity?.bays?.length&&!this.capacity.bays.some((b:any)=>b.bayCode===this.f.bay))this.f.bay=this.capacity.bays[0].bayCode})}
 save(){if(!this.f.vehicleId||!this.f.startAt){this.message='Vehicle and date/time are required.';return;}this.saving=true;this.message='';this.h.post('/api/appointments',{...this.f,startAt:new Date(this.f.startAt).toISOString(),plannedHours:Number(this.f.plannedHours)}).subscribe({next:()=>{this.saving=false;this.createOpen=false;this.reset();this.load()},error:e=>{this.saving=false;this.message=e.error?.message||e.error?.detail||'Unable to save appointment'}})}
 setStatus(x:any,status:string){this.h.put(`/api/appointments/${x.id}/status`,{status}).subscribe(()=>this.load())}
 start(x:any){this.h.post(`/api/appointments/${x.id}/start-service`,{}).subscribe({next:()=>this.load(),error:e=>alert(e.error?.message||'Unable to start service')})}
}