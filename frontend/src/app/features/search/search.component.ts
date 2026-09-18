import { Component,OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { HttpClient } from '@angular/common/http';
import { ActivatedRoute,Router } from '@angular/router';

@Component({
 selector:'app-search',standalone:true,imports:[CommonModule,FormsModule],
 template:`<section class="page">
  <div class="head"><div><h2>Global Search</h2><p>Find a vehicle, VIN, Work Order, Service Event or Maintenance Request from one place.</p></div></div>
  <div class="search"><input [(ngModel)]="q" (keyup.enter)="run()" autofocus placeholder="Search vehicle no., VIN, WO-..., SE-..., MR-..."><button class="btn btn-primary" (click)="run()">Search</button></div>
  <div class="hint">Examples: KA01DA1003 · WO-2026-000003 · VIN · SE-2026-000002</div>
  <div class="empty" *ngIf="searched&&!rows.length">No matching vehicle or service record found.</div>
  <div class="card" *ngIf="rows.length"><table><tr><th>Type</th><th>Reference</th><th>Linked record</th><th>Operational detail</th><th>Status</th><th></th></tr>
   <tr *ngFor="let x of rows"><td><span class="type">{{x.type}}</span></td><td><b>{{x.key}}</b><small>{{x.title}}</small></td><td><b *ngIf="x.linkedReference">{{x.linkedReference}}</b><small *ngIf="x.linkedStatus">{{x.linkedStatus}}</small><span *ngIf="!x.linkedReference">—</span></td><td>{{x.detail||'Open the linked workspace for task, checklist, parts and QC progress.'}}</td><td><span class="status-pill">{{x.status}}</span></td><td><button class="open" (click)="open(x)">Open Workspace</button></td></tr>
  </table></div>
 </section>`,
 styles:[`.page{padding:26px}.head h2{margin:0}.head p{color:#64748b}.search{display:flex;gap:10px;margin:16px 0 6px}.search input{flex:1;padding:12px;border:1px solid #cbd5e1;border-radius:8px;font-size:15px}.btn,.open{border:1px solid #1266d5;border-radius:7px;padding:9px 14px}.btn-primary,.open{background:#1266d5;color:#fff}.hint{font-size:12px;color:#64748b;margin-bottom:14px}.card{background:#fff;border:1px solid #e2e8f0;border-radius:10px;padding:12px}.empty{background:#fff;border:1px solid #e2e8f0;border-radius:8px;padding:16px;margin-top:14px;color:#64748b}table{width:100%;border-collapse:collapse}th,td{padding:10px;border-bottom:1px solid #e5e7eb;text-align:left}.type{background:#eaf3ff;padding:5px 8px;border-radius:12px;font-size:11px}`]
})
export class SearchComponent implements OnInit{
 q='';rows:any[]=[];searched=false;
 constructor(private http:HttpClient,private router:Router,private route:ActivatedRoute){}
 ngOnInit(){this.route.queryParamMap.subscribe(p=>{const q=p.get('q');if(q){this.q=q;this.run()}})}
 run(){const term=this.q.trim();if(!term)return;this.http.get<any>('/api/search',{params:{q:term}}).subscribe(x=>{this.rows=x.results||[];this.searched=true})}
 open(x:any){if(x.url)this.router.navigateByUrl(x.url)}
}
