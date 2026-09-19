import {Component,OnInit} from '@angular/core';
import {CommonModule} from '@angular/common';
import {HttpClient} from '@angular/common/http';
import {FleetDateComponent} from '../../shared/fleet-date.component';
@Component({selector:'app-audit',standalone:true,imports:[CommonModule,FleetDateComponent],template:`
<h2>Audit Trail</h2><p role="status">{{message}}</p><div class="card"><table><thead><tr><th>Occurred On</th><th>User</th><th>Action</th><th>Entity</th><th>Detail</th></tr></thead><tbody><tr *ngFor="let x of rows"><td class="date-cell"><app-fleet-date [value]="x.occurredAt"></app-fleet-date></td><td>{{x.userName}}</td><td>{{x.action}}</td><td>{{x.entityType}}</td><td>{{x.details}}</td></tr></tbody></table></div>`})
export class AuditComponent implements OnInit {
 rows:any[]=[];message='Loading audit records…';
 constructor(private http:HttpClient){}
 ngOnInit(){this.http.get<any[]>('/api/audit').subscribe({next:x=>{this.rows=x;this.message=x.length?'':'No audit records.'},error:()=>this.message='Unable to load audit records.'})}
}
