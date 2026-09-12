import { Component } from '@angular/core';
import { CommonModule } from '@angular/common';

@Component({selector:'app-job-card',standalone:true,imports:[CommonModule],template:`
<section class="page"><div class="title"><div><h2>Job Card JC-2026-00987</h2><p>TN12AB1234 · SE-2026-001234 · Bay-04</p></div><span class="badge blue">In Progress</span></div>
<div class="grid"><div class="card"><h3>Execution</h3><p><b>Technician:</b> Suresh K</p><p><b>HV Authorization:</b> Valid until 31 Dec 2026</p><p><b>Started:</b> 12 Sep 2026 09:44</p><p><b>SRT:</b> 2.8 h</p></div><div class="card"><h3>Closure Controls</h3><p>✓ Diagnosis captured</p><p>✓ Failure code selected</p><p>○ RCA required for repeat failure</p><p>○ QC pending</p></div></div>
<div class="card"><h3>Work Items</h3><table><tr><th>#</th><th>Type</th><th>Description</th><th>SRT</th><th>Checklist</th><th>Status</th></tr><tr *ngFor="let x of rows"><td>{{x.no}}</td><td>{{x.type}}</td><td>{{x.desc}}</td><td>{{x.srt}}</td><td>{{x.check}}</td><td><span class="badge" [ngClass]="x.cls">{{x.status}}</span></td></tr></table></div>
</section>`,styles:[`.page{padding:26px}.title{display:flex;justify-content:space-between;align-items:center}.title h2{margin:0}.title p{color:#64748b}.grid{display:grid;grid-template-columns:1fr 1fr;gap:14px;margin:16px 0}@media(max-width:900px){.grid{grid-template-columns:1fr}}`]})
export class JobCardComponent{rows=[{no:1,type:'PM',desc:'5,000 km PM inspection',srt:'1.5 h',check:'18 / 24',status:'In Progress',cls:'blue'},{no:2,type:'Inspection',desc:'Brake inspection',srt:'0.5 h',check:'6 / 6',status:'Completed',cls:'green'},{no:3,type:'Repair',desc:'Rectify coolant leak',srt:'0.8 h',check:'3 / 5',status:'Waiting Part',cls:'amber'}]}
