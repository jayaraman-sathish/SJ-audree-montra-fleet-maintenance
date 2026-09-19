import {Component,Input,OnChanges,OnDestroy} from '@angular/core';
import {CommonModule} from '@angular/common';
import {FormsModule} from '@angular/forms';
import {HttpClient} from '@angular/common/http';
import {Subscription} from 'rxjs';
@Component({selector:'app-veerai',standalone:true,imports:[CommonModule,FormsModule],template:`
<button class="launch" (click)="openPanel()">✦ Veerai · Analyse this job</button>
<aside *ngIf="open" aria-label="Veerai job analysis" class="panel">
<header><div><h2>Veerai</h2><small>Reason through this service job</small></div><button (click)="close()" aria-label="Close Veerai">×</button></header>
<p>Review possible causes, missing evidence and next checks using this job and earlier repairs on the same vehicle.</p>
<p *ngIf="available===false" role="status">Veerai is not connected. Your administrator must configure AI access.</p>
<div *ngIf="available===true">
<p class="notice">Analysis sends selected job notes and vehicle history to the configured AI provider. No photos or manuals are sent. Review the sources before acting.</p>
<label>Veerai access key<input type="password" autocomplete="off" [(ngModel)]="access" placeholder="Provided by your administrator"></label>
<label>What should Veerai investigate?<textarea [(ngModel)]="question" maxlength="1500" rows="3"></textarea></label>
<div class="suggestions"><button *ngFor="let q of prompts" (click)="question=q" [disabled]="busy">{{q}}</button></div>
<button class="analyse" (click)="analyse()" [disabled]="busy||!access.trim()||!question.trim()">{{busy?'Analysing evidence…':'Analyse this job'}}</button>
<button *ngIf="busy" (click)="cancel()">Cancel</button>
</div>
<p role="alert" *ngIf="error">{{error}}</p>
<div *ngIf="result"><p class="notice">{{result.notice}}</p><small>Analysed {{result.analysedAt|date:'medium'}} · {{result.model}}</small>
<section *ngFor="let s of sections"><h3>{{s.label}}</h3><p *ngIf="!result.analysis[s.key]?.length">No assessment returned for this section.</p>
<article *ngFor="let p of result.analysis[s.key]"><p>{{p.text}}</p><a *ngFor="let id of p.sources" [href]="'#veer-source-'+id" (click)="showSources=true">[{{id}}] </a></article></section>
<button (click)="showSources=!showSources">{{showSources?'Hide':'View'}} evidence sources</button>
<div *ngIf="showSources"><article *ngFor="let s of result.sources" [id]="'veer-source-'+s.id"><b>{{s.id}} · {{s.label}}</b><pre>{{s.detail}}</pre><a [href]="s.url" target="_blank" rel="noopener">Open source job</a></article></div>
</div>
</aside>`,styles:[`
.launch{border:1px solid #b4c7ee;background:#eef4ff;color:#163c75;border-radius:8px;padding:10px 14px;margin-bottom:12px;cursor:pointer}
.panel{position:fixed;right:0;top:0;bottom:0;width:min(590px,100vw);box-sizing:border-box;overflow-y:auto;background:white;box-shadow:-5px 0 24px #172d4d33;z-index:1200;padding:22px;color:#172d4d}
header{position:sticky;top:-22px;background:white;padding:12px 0;z-index:1;display:flex;align-items:center;justify-content:space-between}h2{margin:0}h3{font-size:16px;margin-bottom:8px}label{display:block;margin:12px 0;font-size:13px}input,textarea{box-sizing:border-box;width:100%;padding:10px;border:1px solid #bbc9db;border-radius:6px;margin-top:5px}button{padding:8px 12px;border:1px solid #cbd5e1;background:#fff;border-radius:6px;cursor:pointer}button:disabled{opacity:.55;cursor:wait}.analyse{background:#145bc1;color:white;margin-top:12px}.suggestions{display:flex;gap:6px;flex-wrap:wrap}.suggestions button{font-size:12px}.notice{background:#f0f5fb;padding:10px;font-size:13px}article{border-bottom:1px solid #e0e7f0;padding:8px 0}article p{margin:4px 0;white-space:pre-wrap}pre{white-space:pre-wrap;overflow-wrap:anywhere;font:12px/1.5 monospace}a{color:#155bbb}section{margin-top:18px}
`]})
export class VeeraiComponent implements OnChanges,OnDestroy {
 @Input() jobId='';open=false;available:boolean|null=null;access='';busy=false;error='';result:any=null;showSources=false;
 question='What could explain this complaint, and which checks should the technician do next?';
 prompts=['Investigate possible causes','Check for repeat failures','Review repair and QC evidence'];
 sections=[{key:'findings',label:'Recorded findings'},{key:'possibleCauses',label:'Possible causes — not confirmed'},{key:'missingEvidence',label:'Missing evidence / questions'},{key:'recommendedChecks',label:'Recommended checks'},{key:'qcReview',label:'Repair and QC review'}];
 private request?:Subscription;
 constructor(private http:HttpClient){}
 ngOnChanges(){this.cancel();this.result=null;this.open=false;this.access='';}
 ngOnDestroy(){this.request?.unsubscribe();}
 openPanel(){this.open=true;this.error='';this.http.get<any>('/api/veerai/status').subscribe({next:r=>this.available=r.available,error:()=>{this.available=false;this.error='Unable to check Veerai connection.';}});}
 close(){this.cancel();this.open=false;this.access='';}
 cancel(){this.request?.unsubscribe();this.busy=false;}
 analyse(){if(this.busy||!this.access.trim()||!this.question.trim())return;this.busy=true;this.error='';this.result=null;this.showSources=false;
 this.request=this.http.post<any>('/api/job-cards/'+this.jobId+'/veerai/analyse',{question:this.question},{headers:{'X-Veerai-Access':this.access}}).subscribe({next:r=>{this.result=r;this.busy=false;},error:e=>{this.error=e.status===429?'Veerai is busy. Please wait a minute and retry.':e.error?.message||'Analysis could not be completed. Please retry.';this.busy=false;}});
 }
}
