import { Component } from '@angular/core';
import { CommonModule } from '@angular/common';

@Component({
  selector: 'app-breakdown', standalone: true, imports: [CommonModule],
  template: `
  <section class="page">
    <div class="title"><div><h2>Breakdown / RSA</h2><p>Report, triage, dispatch and restore vehicles with controlled SLA clocks.</p></div><button class="btn btn-primary">+ Report Breakdown</button></div>
    <div class="cards"><div class="card"><span>Open Breakdowns</span><b>7</b></div><div class="card"><span>P1 Critical</span><b class="danger">2</b></div><div class="card"><span>Awaiting Towing</span><b>1</b></div><div class="card"><span>Avg Restore</span><b>2h 18m</b></div></div>
    <div class="card table"><table><tr><th>Breakdown</th><th>Vehicle</th><th>Priority</th><th>Location</th><th>Triage / Dispatch</th><th>Status</th><th>SLA</th></tr>
      <tr *ngFor="let x of rows"><td>{{x.no}}</td><td>{{x.vehicle}}</td><td><span class="badge" [ngClass]="x.priority==='P1'?'red':'amber'">{{x.priority}}</span></td><td>{{x.location}}</td><td>{{x.dispatch}}</td><td>{{x.status}}</td><td>{{x.elapsed}}</td></tr>
    </table></div>
    <div class="flow"><div class="step">1. Breakdown Intake</div><span>→</span><div class="step">2. Triage</div><span>→</span><div class="step">3. RSA / Tow / Workshop</div><span>→</span><div class="step">4. Service Event</div><span>→</span><div class="step">5. Repair</div><span>→</span><div class="step">6. QC / Release</div></div>
  </section>`,
  styles:[`.page{padding:26px}.title{display:flex;justify-content:space-between;align-items:center}.title h2{margin:0}.title p{color:#64748b}.cards{display:grid;grid-template-columns:repeat(4,1fr);gap:12px;margin:16px 0}.cards span{display:block;color:#64748b}.cards b{display:block;font-size:25px;margin-top:6px}.danger{color:#b91c1c}.flow{display:flex;gap:10px;align-items:center;overflow:auto;margin-top:18px}.step{background:#fff7ed;border:1px solid #fed7aa;padding:12px;border-radius:8px;white-space:nowrap}@media(max-width:900px){.cards{grid-template-columns:1fr 1fr}}`]
})
export class BreakdownComponent {
  rows=[
    {no:'BD-2026-00128',vehicle:'TN12AB1234',priority:'P1',location:'Sriperumbudur',dispatch:'Mobile Technician',status:'Assigned',elapsed:'00:42'},
    {no:'BD-2026-00129',vehicle:'KA05EV7782',priority:'P2',location:'Chennai ORR',dispatch:'Tow to Workshop',status:'Awaiting Towing',elapsed:'01:18'},
    {no:'BD-2026-00130',vehicle:'TN22EV9088',priority:'P2',location:'Guindy',dispatch:'Remote Triage',status:'Diagnosis',elapsed:'00:31'}
  ];
}
