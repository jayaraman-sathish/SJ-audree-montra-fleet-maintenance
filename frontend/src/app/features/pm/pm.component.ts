import { Component, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { HttpClient } from '@angular/common/http';
@Component({selector:'app-pm',standalone:true,imports:[CommonModule,FormsModule],template:`
<section class="page"><div class="title"><div><h2>Preventive Maintenance</h2><p>Generate and manage persisted PM obligations</p></div><button class="btn btn-primary" (click)="show=!show">+ Generate Obligation</button></div>
<div class="card form" *ngIf="show"><h3>Generate PM Obligation</h3><div class="fields">
<label>Vehicle<select [(ngModel)]="form.vehicleId"><option value="">Select</option><option *ngFor="let v of vehicles" [value]="v.id">{{v.registrationNumber}}</option></select></label>
<label>Plan Code<input [(ngModel)]="form.planCode"></label><label>Trigger<select [(ngModel)]="form.triggerType"><option>Odometer</option><option>Time</option></select></label>
<label *ngIf="form.triggerType==='Odometer'">Due Reading (km)<input type="number" [(ngModel)]="form.dueReading"></label>
<label *ngIf="form.triggerType==='Time'">Due Date<input type="date" [(ngModel)]="form.dueDate"></label></div>
<button class="btn btn-primary" (click)="generate()">Save</button> <span class="msg">{{message}}</span></div>
<div class="card table"><table><tr><th>Vehicle</th><th>Plan</th><th>Trigger</th><th>Remaining</th><th>Status</th></tr>
<tr *ngFor="let x of rows"><td>{{x.vehicle}}</td><td>{{x.plan}}</td><td>{{x.trigger}}</td><td>{{x.remaining}}</td><td>{{x.status}}</td></tr></table></div></section>`,
styles:[`.page{padding:26px}.title{display:flex;justify-content:space-between;align-items:center}.title h2{margin:0}.title p{color:#64748b}.card{margin-top:16px}.fields{display:grid;grid-template-columns:repeat(3,1fr);gap:12px;margin-bottom:12px}label{font-size:12px;color:#475569}input,select{display:block;width:100%;padding:9px;margin-top:5px;border:1px solid #cbd5e1;border-radius:6px}.msg{margin-left:10px;color:#166534}`]})
export class PmComponent implements OnInit{rows:any[]=[];vehicles:any[]=[];show=false;message='';form:any={vehicleId:'',planCode:'PM-5K',triggerType:'Odometer',dueReading:50000,dueDate:''};constructor(private http:HttpClient){}ngOnInit(){this.load();this.http.get<any[]>('/api/vehicles').subscribe(x=>this.vehicles=x)}load(){this.http.get<any[]>('/api/pm/obligations').subscribe(x=>this.rows=x)}generate(){const b={...this.form,dueDate:this.form.dueDate||null,dueReading:this.form.triggerType==='Odometer'?Number(this.form.dueReading):null};this.http.post('/api/pm/obligations/generate',b).subscribe({next:()=>{this.message='Saved';this.show=false;this.load()},error:e=>this.message=e.error?.message||'Unable to save'})}}
