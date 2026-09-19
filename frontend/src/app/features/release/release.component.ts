import {Component,OnInit} from '@angular/core';
import {CommonModule} from '@angular/common';
import {FormsModule} from '@angular/forms';
import {HttpClient} from '@angular/common/http';
import {RouterLink} from '@angular/router';
import {FleetGridDirective} from '../../shared/fleet-grid.directive';
@Component({selector:'app-release',standalone:true,imports:[CommonModule,FormsModule,RouterLink,FleetGridDirective],template:`
<section class="page"><h2>QC &amp; Vehicle Release</h2><p>Open a service workspace to review pending work, record QC and release the vehicle.</p>
<label>Search vehicle or Job Card<input [(ngModel)]="query" placeholder="Vehicle / Job Card"></label><p *ngIf="loading" role="status">Loading jobs…</p><p *ngIf="error" role="alert">{{error}}</p><button *ngIf="error" (click)="load()">Retry</button>
<div class="card fleet-grid-scroll"><table fleetGrid><tr><th>Job Card</th><th>Service Event</th><th>Vehicle</th><th>Supervisor</th><th>Status</th><th>Action</th></tr><tr *ngFor="let j of filtered()"><td>{{j.jobCardNumber}}</td><td>{{j.eventNumber}}</td><td>{{j.vehicle}}</td><td>{{j.assignedSupervisor||'Unassigned'}}</td><td>{{j.status}}</td><td><a [routerLink]="['/service-workspace',j.id]" [queryParams]="{tab:'release'}">Review QC / Release</a></td></tr></table><p *ngIf="!loading&&!error&&!filtered().length">No matching active service jobs.</p></div></section>`,styles:[`.page{padding:24px}label{display:grid;gap:6px;max-width:480px}input{padding:10px;border:1px solid #cbd5e1;border-radius:8px}.card{margin-top:16px}table{width:100%;border-collapse:collapse}td,th{padding:12px;text-align:left;border-bottom:1px solid #e2e8f0}`]})
export class ReleaseComponent implements OnInit {
 rows:any[]=[];query='';loading=false;error='';
 constructor(private http:HttpClient){}
 ngOnInit(){this.load()}
 load(){this.loading=true;this.error='';this.http.get<any[]>('/api/job-cards').subscribe({next:rows=>{this.rows=rows;this.loading=false},error:()=>{this.loading=false;this.error='Unable to load service jobs. Please retry.'}})}
 filtered(){const q=this.query.trim().toLowerCase();return this.rows.filter(j=>!['Completed','Cancelled','Closed'].includes(j.status)&&`${j.vehicle} ${j.jobCardNumber} ${j.eventNumber}`.toLowerCase().includes(q))}
}
