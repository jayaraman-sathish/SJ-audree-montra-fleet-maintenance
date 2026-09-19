import { FleetDateComponent } from '../../shared/fleet-date.component';
import { Component, OnInit, OnDestroy } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { HttpClient } from '@angular/common/http';
import { ActivatedRoute, Router } from '@angular/router';
import { Subscription } from 'rxjs';

@Component({
 selector:'app-search', standalone:true, imports:[FleetDateComponent,CommonModule,FormsModule],
 template:`<section class="page">
  <h2>Global Search</h2><p class="muted">Search a vehicle or reference to see each service visit and its linked Job Card together.</p>
  <form class="search" (ngSubmit)="run()"><input name="query" [(ngModel)]="q" placeholder="Vehicle, VIN, Job Card, Service Event, breakdown or complaint" aria-label="Search fleet records"><button type="submit">Search</button></form>
  <p role="status" *ngIf="loading">Loading service records…</p>
  <p role="alert" class="error" *ngIf="error">{{error}}</p>
  <p *ngIf="searched&&!loading&&!error&&!rows.length">No matching records found.</p>
  <p *ngIf="limitReached" class="muted">Showing the latest 30 matching visits. Search a specific reference to narrow the results.</p>
  <article class="record" *ngFor="let x of rows">
   <header><div><span class="type">{{x.type}}</span><h3>{{x.key}}</h3><p>{{x.title}}</p></div><span class="status">{{x.status}}</span></header>
   <ng-container *ngIf="x.type==='Service Visit';else otherRecord">
    <div class="date-field"><span>Service Opened On</span><app-fleet-date [value]="x.openedAt"></app-fleet-date></div>
    <section class="issues"><h4>Reported issue / service reason</h4>
     <p *ngIf="!x.issues?.length">{{x.issueSummary}}</p>
     <div class="issue" *ngFor="let issue of x.issues"><strong>{{issue.reference}}</strong><p>{{issue.description}}</p><div class="date-field"><span>Reported On</span><app-fleet-date [value]="issue.reportedAt"></app-fleet-date></div></div>
    </section>
    <p *ngIf="!x.jobs?.length">No Job Card has been created for this service visit.</p>
    <section class="job" *ngFor="let job of x.jobs">
     <div class="job-head"><div><span class="muted">Linked Job Card</span><h3>{{job.number}}</h3><p class="stage">{{job.stage}}</p></div><div class="record-action"><span>Action</span><button (click)="open(job)">Open Workspace</button></div></div>
     <div class="metrics">
      <div><span>Assigned To (Supervisor)</span><strong>{{job.assignedSupervisor||'Unassigned'}}</strong></div><div><span>Supervisor Assigned On</span><app-fleet-date [value]="job.supervisorAssignedAt"></app-fleet-date></div><div><span>Internal technician</span><strong>{{job.engineer}}</strong><small *ngIf="job.taskEngineers?.length">Task engineers: {{job.taskEngineers.join(', ')}}</small></div>
      <div><span>Bay</span><strong>{{job.bay}}</strong></div>
      <div><span>Tasks completed</span><strong>{{job.tasksCompleted}} / {{job.tasksTotal}}</strong><progress *ngIf="job.tasksTotal" [value]="job.tasksCompleted" [max]="job.tasksTotal" aria-label="Tasks completed"></progress></div>
      <div><span>Checklist items recorded</span><strong>{{job.checksRecorded}} / {{job.checksTotal}}</strong><small>Recorded results may include Not OK.</small></div>
      <div><span>Parts requests awaiting issue</span><strong>{{job.partsWaiting}}</strong></div>
      <div><span>QC status</span><strong>{{job.qcStatus}}</strong></div>
     </div>
     <details *ngIf="job.additionalWork?.length"><summary>Additional work — {{job.additionalWork.length}} task(s) under {{job.number}}</summary>
      <div class="additional" *ngFor="let t of job.additionalWork"><strong>{{t.taskCode}}</strong><p>{{t.description}}</p><span>{{t.status}}</span></div>
     </details>
     <p class="muted footnote">This Job Card belongs to {{x.key}}. Open Workspace → History to see the recorded timeline.</p>
    </section>
   </ng-container>
   <ng-template #otherRecord><div class="date-field" *ngIf="x.reportedAt"><span>Reported On</span><app-fleet-date [value]="x.reportedAt"></app-fleet-date></div><button (click)="open(x)">{{x.type==='Vehicle'?'View Vehicle 360':'View Requests'}}</button></ng-template>
  </article>
 </section>`,
 styles:[`.page{padding:26px;max-width:1500px;margin:auto}h2{margin:0}h3{margin:6px 0;font-size:18px}h4{margin:0 0 8px}p{margin:6px 0;line-height:1.5}.muted,small{color:#586b83}.search{display:flex;gap:10px;margin:20px 0}.search input{flex:1;min-width:0;padding:12px;border:1px solid #bdcbdd;border-radius:8px;font-size:16px}button{min-height:46px;background:#1266d5;color:white;border:0;border-radius:8px;padding:10px 18px;cursor:pointer;font-weight:600}.record{border:1px solid #dce4ee;background:white;border-radius:12px;padding:20px;margin:16px 0}.record header,.job-head{display:flex;justify-content:space-between;align-items:flex-start;gap:16px}.type,.status{display:inline-block;background:#eaf3ff;padding:6px 10px;border-radius:16px;font-size:13px}.status{white-space:nowrap}.issues{background:#f6f8fb;border-radius:8px;padding:14px;margin:14px 0}.issue+.issue{border-top:1px solid #dce4ee;margin-top:10px;padding-top:10px}.issue p{white-space:pre-wrap;overflow-wrap:anywhere}.job{border-top:1px solid #dce4ee;padding-top:16px;margin-top:16px}.stage{font-weight:600;color:#244e78}.metrics{display:grid;grid-template-columns:repeat(3,minmax(0,1fr));gap:12px;margin:16px 0}.metrics>div{background:#f6f8fb;border-radius:8px;padding:12px}.metrics span,.metrics strong,.metrics small{display:block}.metrics span{font-size:13px;color:#586b83}.metrics strong{margin-top:6px;font-size:17px}.metrics small{margin-top:5px}progress{width:100%;height:8px;margin-top:8px}summary{cursor:pointer;padding:12px 0;min-height:24px;font-weight:600}.additional{padding:10px;border-top:1px solid #dce4ee}.footnote{font-size:12px}.error{color:#a71919}button:focus-visible,input:focus-visible,summary:focus-visible{outline:3px solid #ea9b35;outline-offset:3px}@media(max-width:850px){.metrics{grid-template-columns:repeat(2,minmax(0,1fr))}}@media(max-width:520px){.page{padding:12px}.record{padding:14px}.search{flex-direction:column}.record header,.job-head{flex-wrap:wrap}.job-head button{width:100%}.metrics{grid-template-columns:1fr}.status{white-space:normal}}`]
})
export class SearchComponent implements OnInit,OnDestroy {
 q=''; rows:any[]=[]; searched=false; loading=false; error=''; limitReached=false;
 private request?:Subscription; private routeSubscription?:Subscription;
 constructor(private http:HttpClient,private router:Router,private route:ActivatedRoute){}
 ngOnInit(){this.routeSubscription=this.route.queryParamMap.subscribe(p=>{this.q=p.get('q')||'';if(this.q.trim())this.run();});}
 run(){this.request?.unsubscribe();const term=this.q.trim();this.error='';this.rows=[];this.limitReached=false;
  if(!term){this.loading=false;this.searched=false;return;}
  this.loading=true;this.searched=true;
  this.request=this.http.get<any>('/api/search',{params:{q:term}}).subscribe({
   next:r=>{this.rows=r.results||[];this.limitReached=!!r.visitLimitReached;this.loading=false;},
   error:()=>{this.error='Unable to load fleet records. Please try again.';this.loading=false;}
  });
 }
 open(x:any){if(x.url)this.router.navigateByUrl(x.url);}
 ngOnDestroy(){this.request?.unsubscribe();this.routeSubscription?.unsubscribe();}
}
