import {Component,Input,OnChanges,OnDestroy,HostListener} from '@angular/core';
import {CommonModule} from '@angular/common';
import {FormsModule} from '@angular/forms';
import {HttpClient} from '@angular/common/http';
import {Subscription} from 'rxjs';
@Component({selector:'app-veerai',standalone:true,imports:[CommonModule,FormsModule],template:`
<button *ngIf="!open" class="launch" (click)="openPanel()" aria-label="Open Veerai">✦ {{jobId?'Veerai · Analyse this job':'Ask Veerai'}}</button>
<aside *ngIf="open" aria-label="Veerai job analysis" class="panel">
<header><div><h2>Veerai</h2><small>Reason through this service job</small></div><button (click)="close()" aria-label="Close Veerai">×</button></header>
<p>Choose a service job, then ask Veerai what to investigate.</p>
<div class="job-picker" *ngIf="!jobId">
<label>Find vehicle or Job Card<input [(ngModel)]="jobSearch" (ngModelChange)="searchChanged()" placeholder="Vehicle number / Job Card" aria-label="Find Veerai job"></label>
<label *ngIf="filteredJobs().length">Choose a matching service job<select [(ngModel)]="selectedJobId" (ngModelChange)="changeJob()" aria-label="Veerai job"><option value="">Select a service job</option><option *ngFor="let j of filteredJobs()" [value]="j.id">{{j.vehicle}} · {{j.jobCardNumber}} · {{j.status}}</option></select></label>
<p *ngIf="loadingJobs">Loading service jobs…</p><p *ngIf="jobError" role="alert">{{jobError}} <button (click)="loadJobs()">Retry</button></p><p *ngIf="!loadingJobs&&!jobError&&!filteredJobs().length">No service jobs match “{{jobSearch}}”. Try the last few registration digits. A vehicle must have a Job Card before Veerai can analyse it. <button (click)="clearSearch()">Show all service jobs</button></p>
<p class="selected-job" *ngIf="selectedJob">Selected: <b>{{selectedJob.vehicle}}</b> · {{selectedJob.jobCardNumber}} · {{selectedJob.status}}</p>
</div>
<p *ngIf="jobId">Using the current Service Workspace.</p>
<p *ngIf="available===false" role="status">Veerai is not connected. Your administrator must configure AI access.</p>
<div *ngIf="available===true">
<p class="notice">Selected job notes and vehicle history are sent to AI. No photos or manuals. Check the evidence before acting.</p>
<label>Veerai access key<input type="password" autocomplete="off" [(ngModel)]="access" placeholder="Enter your Veerai access key"><small>Use the Veerai key supplied by your administrator, not the OpenAI API key.</small></label>
<label>What should Veerai investigate?<textarea [(ngModel)]="question" maxlength="1500" rows="3"></textarea></label>
<div class="suggestions"><button *ngFor="let q of prompts" (click)="question=q" [disabled]="busy">{{q}}</button></div>
<div class="submit-area"><p *ngIf="blockedReason" role="status" id="veerai-submit-help">{{blockedReason}}</p>
<button aria-describedby="veerai-submit-help" class="analyse" (click)="analyse()" [disabled]="busy||!activeJobId||!access.trim()||!question.trim()">{{busy?'Analysing evidence…':'Analyse this job'}}</button>
<button *ngIf="busy" (click)="cancel()">Cancel</button></div>
</div>
<p role="alert" *ngIf="error">{{error}}</p>
<div *ngIf="result"><p class="notice">{{result.notice}}</p><small>Analysed {{result.analysedAt|date:'medium'}} · {{result.model}}</small>
<section *ngFor="let s of sections"><h3>{{s.label}}</h3><p *ngIf="!result.analysis[s.key]?.length">No assessment returned for this section.</p>
<article *ngFor="let p of result.analysis[s.key]"><p>{{p.text}}</p><a *ngFor="let id of p.sources" [href]="'#veer-source-'+id" (click)="showSources=true">[{{id}}] </a></article></section>
<button (click)="showSources=!showSources">{{showSources?'Hide':'View'}} evidence sources</button>
<div *ngIf="showSources"><article *ngFor="let s of result.sources" [id]="'veer-source-'+s.id"><b>{{s.id}} · {{s.label}}</b><pre>{{s.detail}}</pre><a [href]="s.url" target="_blank" rel="noopener">Open source job</a></article></div>
</div>
</aside>`,styles:[`
.launch{position:fixed;bottom:22px;right:24px;z-index:100;box-shadow:0 4px 16px #17375d33;border:1px solid #b4c7ee;background:#eef4ff;color:#163c75;border-radius:8px;padding:10px 14px;margin-bottom:12px;cursor:pointer}
.panel{position:fixed;right:0;top:0;bottom:0;width:min(590px,100vw);box-sizing:border-box;overflow-y:auto;background:white;box-shadow:-5px 0 24px #172d4d33;z-index:1200;padding:22px;color:#172d4d}
header{position:sticky;top:-22px;background:white;padding:12px 0;z-index:1;display:flex;align-items:center;justify-content:space-between}h2{margin:0}h3{font-size:16px;margin-bottom:8px}label{display:block;margin:12px 0;font-size:13px}input,textarea,select{box-sizing:border-box;width:100%;padding:10px;border:1px solid #bbc9db;border-radius:6px;margin-top:5px}button{padding:8px 12px;border:1px solid #cbd5e1;background:#fff;border-radius:6px;cursor:pointer}button:disabled{opacity:.55;cursor:not-allowed}.analyse{background:#145bc1;color:white;margin-top:12px}.selected-job{padding:10px;background:#edf8f1;border:1px solid #badbc5;border-radius:6px}.submit-area{position:sticky;bottom:-22px;background:white;padding:10px 0;border-top:1px solid #e0e7f0;margin-top:12px;z-index:1}.submit-area p{font-size:13px;margin:0;color:#75420b}.submit-area .analyse{width:100%;min-height:44px}.suggestions{display:flex;gap:6px;flex-wrap:wrap}.suggestions button{font-size:12px}.notice{background:#f0f5fb;padding:10px;font-size:13px}article{border-bottom:1px solid #e0e7f0;padding:8px 0}article p{margin:4px 0;white-space:pre-wrap}pre{white-space:pre-wrap;overflow-wrap:anywhere;font:12px/1.5 monospace}a{color:#155bbb}section{margin-top:18px}
`]})
export class VeeraiComponent implements OnChanges,OnDestroy {
 @Input() jobId='';open=false;available:boolean|null=null;access='';busy=false;error='';result:any=null;showSources=false;
 question='What could explain this complaint, and which checks should the technician do next?';
 prompts=['Investigate possible causes','Check for repeat failures','Review repair and QC evidence'];
 sections=[{key:'findings',label:'Recorded findings'},{key:'possibleCauses',label:'Possible causes — not confirmed'},{key:'missingEvidence',label:'Missing evidence / questions'},{key:'recommendedChecks',label:'Recommended checks'},{key:'qcReview',label:'Repair and QC review'}];
 selectedJobId='';jobSearch='';jobs:any[]=[];loadingJobs=false;jobError='';
 private request?:Subscription;private jobsRequest?:Subscription;private statusRequest?:Subscription;
 get activeJobId(){return this.jobId||this.selectedJobId;}
 @HostListener('document:keydown.escape') escape(){if(this.open)this.close();}
 normalize(value:string){return (value||'').toLowerCase().replace(/[^a-z0-9]/g,'');}
 filteredJobs(){const q=this.normalize(this.jobSearch);return this.jobs.filter(j=>this.normalize(j.vehicle).includes(q)||this.normalize(j.jobCardNumber).includes(q));}
 get selectedJob(){return this.jobs.find(j=>j.id===this.selectedJobId);}
 get blockedReason(){if(this.busy)return '';if(!this.activeJobId)return 'Choose a service job to continue.';if(!this.access.trim())return 'Enter your Veerai access key to continue.';if(!this.question.trim())return 'Enter a question or choose one below.';return '';}
 searchChanged(){this.selectedJobId='';this.changeJob();}
 clearSearch(){this.jobSearch='';this.searchChanged();}
 changeJob(){this.cancel();this.result=null;this.error='';this.showSources=false;}
 loadJobs(){this.jobsRequest?.unsubscribe();this.loadingJobs=true;this.jobError='';this.jobsRequest=this.http.get<any[]>('/api/job-cards').subscribe({next:r=>{this.jobs=r;this.loadingJobs=false;},error:()=>{this.loadingJobs=false;this.jobError='Unable to load service jobs.';}});}
 constructor(private http:HttpClient){}
 ngOnChanges(){this.cancel();this.result=null;this.open=false;this.access='';this.selectedJobId='';}
 ngOnDestroy(){this.request?.unsubscribe();this.jobsRequest?.unsubscribe();this.statusRequest?.unsubscribe();}
 openPanel(){this.open=true;this.error='';if(!this.jobId)this.loadJobs();this.statusRequest?.unsubscribe();this.statusRequest=this.http.get<any>('/api/veerai/status').subscribe({next:r=>this.available=r.available,error:()=>{this.available=false;this.error='Unable to check Veerai connection.';}});}
 close(){this.cancel();this.open=false;this.access='';}
 cancel(){this.request?.unsubscribe();this.busy=false;}
 analyse(){if(this.busy||!this.activeJobId||!this.access.trim()||!this.question.trim())return;this.busy=true;this.error='';this.result=null;this.showSources=false;
 this.request=this.http.post<any>('/api/job-cards/'+this.activeJobId+'/veerai/analyse',{question:this.question},{headers:{'X-Veerai-Access':this.access}}).subscribe({next:r=>{this.result=r;this.busy=false;},error:e=>{this.error=e.status===429?'Veerai is busy. Please wait a minute and retry.':e.error?.message||'Analysis could not be completed. Please retry.';this.busy=false;}});
 }
}
