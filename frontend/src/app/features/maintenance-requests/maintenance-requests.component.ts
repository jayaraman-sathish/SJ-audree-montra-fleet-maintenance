import {SupervisorSelectComponent} from '../../shared/supervisor-select.component';
import {Router} from '@angular/router';
import {FleetGridDirective} from '../../shared/fleet-grid.directive';
import { FleetDateComponent } from '../../shared/fleet-date.component';
import {Component,OnInit} from '@angular/core';
import {CommonModule} from '@angular/common';
import {FormsModule} from '@angular/forms';
import {HttpClient} from '@angular/common/http';

@Component({selector:'app-maintenance-requests',standalone:true,imports:[SupervisorSelectComponent,FleetGridDirective,FleetDateComponent,CommonModule,FormsModule],template:`
<section class="page">
<div class="title"><div><h2>Maintenance Requests</h2><p>Unplanned or manually reported service needs. Planned preventive maintenance is generated under PM Obligations.</p></div><button class="btn btn-primary" (click)="open()">+ Maintenance Request</button></div>
<div class="note"><b>Request = why service is needed.</b> Complaint category selects the diagnostic checklist. Appointment = when/where. Check-In = vehicle arrived. Work Order = workshop execution.</div>

<div class="modal-backdrop" *ngIf="show||planOpen||assignOpen"></div>
<div class="modal-card" *ngIf="show"><div class="modal-head"><div><h3>Create Maintenance Request</h3><p>Classify the complaint so the correct diagnostic checklist is generated automatically.</p></div><button class="icon-btn" (click)="show=false">×</button></div>
<div class="fields">
<label>Vehicle<select [(ngModel)]="f.vehicleId"><option *ngFor="let v of vehicles" [value]="v.id">{{v.registrationNumber}} · {{v.model}}</option></select></label>
<label>Source<select [(ngModel)]="f.sourceType"><option>Manual</option><option>Customer</option><option>Inspection</option><option>Campaign</option></select></label>
<label>Type<select [(ngModel)]="f.requestType"><option>Repair</option><option>Inspection</option><option>Service</option><option>Campaign</option></select></label>
<label>Priority<select [(ngModel)]="f.priority"><option>P1</option><option>P2</option><option>P3</option><option>P4</option></select></label>
<label>Complaint Category<select [(ngModel)]="f.complaintCategoryCode"><option *ngFor="let x of categories" [value]="x.code">{{x.name}}</option></select><small>{{selectedCategory()?.description}}</small></label>
<label>Symptom<select [(ngModel)]="f.symptomCode"><option *ngFor="let x of symptoms" [value]="x.code">{{x.name}}</option></select></label>
<label class="wide">Complaint / Description<textarea [(ngModel)]="f.description" placeholder="Example: oil leak observed below vehicle after parking"></textarea></label>
<label>Service timing<select [(ngModel)]="immediate"><option [ngValue]="true">Vehicle here — create & assign now</option><option [ngValue]="false">Report only — schedule later</option></select></label><label *ngIf="immediate">Assigned supervisor<app-supervisor-select [(value)]="assignedSupervisor"></app-supervisor-select></label><label>Target Date<input type="date" [(ngModel)]="target"></label>
<label>Diagnostic Template<input [value]="selectedCategory()?.templateCode||'DIAG-GENERAL'" disabled></label>
</div>
<div class="actions"><button class="btn btn-outline" (click)="show=false">Cancel</button><button class="btn btn-primary" [disabled]="saving" (click)="save()">{{saving?'Saving…':immediate?'Create & Assign':'Save Request'}}</button></div><div class="error">{{message}}</div></div>

<div class="modal-card" *ngIf="assignOpen"><h3>Create & Assign Service</h3><p>{{selected?.requestNumber}} · {{selected?.description}}</p><label>Assigned supervisor<app-supervisor-select [(value)]="assignedSupervisor"></app-supervisor-select></label><div class="actions"><button class="btn" [disabled]="saving" (click)="assignOpen=false">Cancel</button><button class="btn btn-primary" [disabled]="saving||!assignedSupervisor.trim()" (click)="createExisting()">Create & Assign</button></div><p role="alert">{{message}}</p></div>
<div class="modal-card checkin" *ngIf="planOpen"><div class="modal-head"><div><h3>{{checkInNow?'Check In Vehicle':'Schedule Appointment'}}</h3><p>{{selected?.requestNumber}} · {{selected?.vehicle}} · {{selected?.description}}</p></div><button class="icon-btn" (click)="closePlan()">×</button></div>
<div class="fields">
<label *ngIf="!checkInNow">Date / Time<input type="datetime-local" [(ngModel)]="appointment.startAt" (change)="loadCapacity()"></label>
<label>Planned Hours<input type="number" min="0.5" step="0.5" [(ngModel)]="appointment.plannedHours"></label>
<label>Service Centre<select [(ngModel)]="appointment.serviceCentre" (change)="serviceCentreChanged()"><option value="">Select service centre</option><option *ngFor="let c of centres" [value]="c.centreCode">{{c.name}} · {{c.city}}</option></select></label>
<label>Bay<select [(ngModel)]="appointment.bay"><option value="">Select bay</option><option *ngFor="let b of capacity?.bays" [value]="b.bayCode">{{b.bayCode}} · {{b.bayType}} · {{b.bookedHours}}h booked</option></select></label>

<ng-container *ngIf="checkInNow">
<label>Actual Odometer (km)<input type="number" [(ngModel)]="checkin.odometerKm"></label>
<label>Operating Hours<input type="number" [(ngModel)]="checkin.operatingHours"></label>
<label>Energy Used (kWh)<input type="number" [(ngModel)]="checkin.energyKwh"></label>
<label class="wide">Additional Complaint<input [(ngModel)]="checkin.additionalComplaint" placeholder="Any new complaint reported at vehicle arrival"></label>
<label class="wide">Arrival Remarks<textarea [(ngModel)]="checkin.arrivalRemarks" placeholder="Vehicle condition / arrival notes"></textarea></label>
</ng-container>

<label class="wide">Service Reason<input [(ngModel)]="appointment.reason"></label>
</div>
<div class="template-note" *ngIf="selected"><b>Execution checklist:</b> {{selected.diagnosticTemplateCode||'DIAG-GENERAL'}} · {{selected.complaintCategoryCode||'GENERAL'}}. If no specific mapping exists, General Diagnosis is generated so the Work Order is never blank.</div>
<div class="actions"><button class="btn btn-outline" (click)="closePlan()">Cancel</button><button class="btn btn-primary" [disabled]="saving" (click)="saveAppointment()">{{saving?'Saving...':(checkInNow?'Create & Check In':'Schedule Appointment')}}</button></div><div class="error">{{message}}</div></div>

<div class="card"><div class="fleet-grid-scroll" role="region" aria-label="Records" tabindex="0"><table fleetGrid><tr><th>Request</th><th>Requested On</th><th>Target Date</th><th>Vehicle</th><th>Source</th><th>Type</th><th>Complaint</th><th>Priority</th><th>Description</th><th>Status</th><th>Action</th></tr>
<tr *ngFor="let x of rows"><td>{{x.requestNumber}}</td><td class="date-cell"><app-fleet-date [value]="x.requestedAt"></app-fleet-date></td><td class="date-cell"><app-fleet-date [value]="x.targetDate" [dateOnly]="true"></app-fleet-date></td><td>{{x.vehicle}}</td><td>{{x.sourceType}}</td><td>{{x.requestType}}</td><td><b>{{categoryName(x.complaintCategoryCode)}}</b><small>{{symptomName(x.symptomCode)}} · {{x.diagnosticTemplateCode}}</small></td><td>{{x.priority}}</td><td>{{x.description}}</td><td>{{x.status}}</td><td class="act"><button *ngIf="x.status==='Open'" class="btn btn-primary" (click)="schedule(x,false)">Schedule Appointment</button><button *ngIf="x.status==='Open'" class="btn btn-outline" (click)="assignExisting(x)">Create & Assign</button><button *ngIf="x.status==='Open'" class="btn btn-outline" (click)="setStatus(x,'Cancelled')">Cancel</button><span *ngIf="x.status!=='Open'">{{x.status}}</span></td></tr>
<tr *ngIf="!rows.length"><td colspan="11" class="empty">No maintenance requests.</td></tr></table></div></div>
</section>`,styles:[`.page{padding:26px}.title{display:flex;justify-content:space-between;align-items:center}.title p{color:#64748b}.note{background:#eef6ff;border:1px solid #bfdbfe;border-radius:8px;padding:10px 12px;margin:12px 0}.card{margin-top:14px}.modal-backdrop{position:fixed;inset:0;background:rgba(15,23,42,.35);z-index:1000}.modal-card{position:fixed;z-index:1001;left:50%;top:50%;transform:translate(-50%,-50%);width:min(860px,94vw);max-height:88vh;overflow:auto;background:#fff;border-radius:12px;padding:20px;box-shadow:0 20px 60px rgba(0,0,0,.2)}.modal-card.checkin{width:min(900px,94vw)}.modal-head{display:flex;justify-content:space-between}.modal-head h3{margin:0}.modal-head p{margin:4px 0;color:#64748b}.icon-btn{border:0;background:transparent;font-size:28px}.fields{display:grid;grid-template-columns:repeat(2,1fr);gap:12px;margin:16px 0}.wide{grid-column:1/-1}label{font-size:12px}label small,td small{display:block;color:#64748b;margin-top:4px}input,select,textarea{display:block;width:100%;box-sizing:border-box;padding:9px;margin-top:4px;border:1px solid #cbd5e1;border-radius:6px}textarea{min-height:60px}.actions{display:flex;justify-content:flex-end;gap:8px}.error{color:#b91c1c}.empty{text-align:center;color:#64748b;padding:18px}.act{display:flex;gap:5px;flex-wrap:wrap}.btn{white-space:nowrap}.template-note{background:#f8fafc;border:1px solid #e2e8f0;padding:10px;border-radius:8px;font-size:12px;margin-bottom:12px}@media(max-width:800px){.fields{grid-template-columns:1fr}.wide{grid-column:auto}}`]})
export class MaintenanceRequestsComponent implements OnInit{
 assignOpen=false;immediate=true;assignedSupervisor='';
 rows:any[]=[];vehicles:any[]=[];categories:any[]=[];symptoms:any[]=[];centres:any[]=[];show=false;planOpen=false;message='';target='';selected:any=null;checkInNow=false;saving=false;capacity:any;
 f:any={vehicleId:'',sourceType:'Manual',sourceReference:'',requestType:'Repair',complaintCategoryCode:'GENERAL',symptomCode:'OTHER',priority:'P3',description:'',requestedBy:'Fleet User',targetDate:null};
 appointment:any={startAt:'',serviceCentre:'',bay:'',plannedHours:2,reason:''};
 checkin:any={odometerKm:null,operatingHours:null,energyKwh:null,additionalComplaint:'',arrivalRemarks:''};
 constructor(private h:HttpClient,private router:Router){}
 ngOnInit(){
  this.h.get<any[]>('/api/vehicles').subscribe(x=>{this.vehicles=x;if(x.length)this.f.vehicleId=x[0].id});
  this.h.get<any>('/api/maintenance-requests/config').subscribe(x=>{this.categories=x.categories||[];this.symptoms=x.symptoms||[];this.centres=x.centres||[]});
  this.load()
 }
 load(){this.h.get<any[]>('/api/maintenance-requests').subscribe(x=>this.rows=x)}
 open(){this.assignedSupervisor='';this.immediate=true;this.message='';this.f={vehicleId:this.vehicles[0]?.id||'',sourceType:'Manual',sourceReference:'',requestType:'Repair',complaintCategoryCode:'GENERAL',symptomCode:'OTHER',priority:'P3',description:'',requestedBy:'Fleet User',targetDate:null};this.target='';this.show=true}
 selectedCategory(){return this.categories.find(x=>x.code===this.f.complaintCategoryCode)}
 categoryName(code:string){return this.categories.find(x=>x.code===code)?.name||code||'General'}
 symptomName(code:string){return this.symptoms.find(x=>x.code===code)?.name||code||'Other'}
 save(){if(this.saving)return;if(!this.f.vehicleId||!this.f.description.trim()||(this.immediate&&!this.assignedSupervisor.trim())){this.message='Enter the vehicle, complaint and supervisor for immediate service.';return}this.saving=true;this.f.targetDate=this.target?new Date(this.target).toISOString():null;this.h.post<any>('/api/maintenance-requests',{...this.f,assignedSupervisor:this.immediate?this.assignedSupervisor.trim():null}).subscribe({next:r=>{this.saving=false;this.show=false;if(r.jobCardId)this.router.navigate(['/service-workspace',r.jobCardId]);else this.load()},error:e=>{this.saving=false;this.message=e.error?.message||'Unable to save request'}})}
 assignExisting(x:any){this.selected=x;this.assignedSupervisor='';this.assignOpen=true;this.message=''}
 createExisting(){if(this.saving||!this.assignedSupervisor.trim())return;this.saving=true;this.h.post<any>('/api/work-orders/from-requests',{requestIds:[this.selected.id],priority:this.selected.priority,bay:'',technicianId:null,technician:'',createdBy:'Service Advisor',assignedSupervisor:this.assignedSupervisor.trim()}).subscribe({next:r=>{this.saving=false;this.router.navigate(['/service-workspace',r.workOrder.id])},error:e=>{this.saving=false;this.message=e.error?.message||'Unable to create service'}})}

 vehicle(id:string){return this.vehicles.find(v=>v.id===id)}
 schedule(x:any,now:boolean){
  this.selected=x;this.checkInNow=now;const v=this.vehicle(x.vehicleId);const defaultCentre=v?.serviceCentreCode||'';
  this.appointment={startAt:now?this.localDateTime():'',serviceCentre:defaultCentre,bay:'',plannedHours:2,reason:x.description};
  this.checkin={odometerKm:v?.odometerKm??null,operatingHours:v?.operatingHours??null,energyKwh:v?.energyKwh??null,additionalComplaint:'',arrivalRemarks:''};
  this.capacity=null;this.message='';this.planOpen=true;if(now||defaultCentre)this.loadCapacity()
 }
 localDateTime(){const d=new Date();const z=new Date(d.getTime()-d.getTimezoneOffset()*60000);return z.toISOString().slice(0,16)}
 closePlan(){if(!this.saving)this.planOpen=false}
 serviceCentreChanged(){this.appointment.bay='';this.loadCapacity()}
 loadCapacity(){const start=this.appointment.startAt||this.localDateTime();if(!this.appointment.serviceCentre){this.capacity=null;return}const d=start.substring(0,10);this.h.get('/api/capacity',{params:{date:d,serviceCentre:this.appointment.serviceCentre}}).subscribe(x=>{this.capacity=x;const bays=this.capacity?.bays||[];if(bays.length&&!this.appointment.bay)this.appointment.bay=bays[0].bayCode})}
 saveAppointment(){
  if(!this.selected)return;
  if(!this.appointment.startAt){this.message='Appointment date/time is required.';return}
  if(!this.appointment.serviceCentre){this.message='Select a service centre.';return}
  if(!this.appointment.bay){this.message='Select a service bay.';return}
  this.saving=true;this.message='';
  const body={vehicleId:this.selected.vehicleId,pmObligationId:null,sourceType:'Maintenance Request',sourceReference:this.selected.id,startAt:new Date(this.appointment.startAt).toISOString(),serviceCentre:this.appointment.serviceCentre,bay:this.appointment.bay,technicianId:null,appointmentType:this.selected.requestType,priority:this.selected.priority,reason:this.appointment.reason,plannedHours:Number(this.appointment.plannedHours)||2,createdBy:'Service Advisor'};
  this.h.post<any>('/api/appointments',body).subscribe({next:a=>{
    const finish=(status:string)=>this.h.put(`/api/maintenance-requests/${this.selected.id}/status`,{status}).subscribe({next:()=>{this.saving=false;this.planOpen=false;this.load()},error:()=>{this.saving=false;this.planOpen=false;this.load()}});
    if(this.checkInNow){
      const checkBody={serviceCentre:this.appointment.serviceCentre,bay:this.appointment.bay,odometerKm:this.checkin.odometerKm,operatingHours:this.checkin.operatingHours,energyKwh:this.checkin.energyKwh,additionalComplaint:this.checkin.additionalComplaint||'',arrivalRemarks:this.checkin.arrivalRemarks||''};
      this.h.post(`/api/appointments/${a.id}/check-in`,checkBody).subscribe({next:()=>finish('Vehicle Arrived'),error:e=>{this.saving=false;this.message=e.error?.message||'Unable to check in vehicle'}});
    } else finish('Scheduled');
  },error:e=>{this.saving=false;this.message=e.error?.message||'Unable to create appointment'}})
 }
 setStatus(x:any,status:string){this.h.put(`/api/maintenance-requests/${x.id}/status`,{status}).subscribe(()=>this.load())}
}
