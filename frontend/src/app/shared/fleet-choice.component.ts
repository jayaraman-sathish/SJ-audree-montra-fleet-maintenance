import {Component,Input,Output,EventEmitter} from '@angular/core';
import {CommonModule} from '@angular/common';
let nextChoiceId=0;
@Component({selector:'app-fleet-choice',standalone:true,imports:[CommonModule],template:`
 <fieldset [disabled]="disabled"><legend>{{label}}</legend><div class="choices">
  <label *ngFor="let option of options" [class.selected]="value===option" [class.positive]="value===option&&(option==='OK'||option==='Pass')" [class.negative]="value===option&&(option==='Not OK'||option==='Fail')">
   <input type="radio" [name]="group" [value]="option" [checked]="value===option" (change)="valueChange.emit(option)"><span>{{option}}</span><span *ngIf="value===option" class="tick" aria-hidden="true">✓</span>
  </label>
 </div></fieldset>
 `,styles:[`
 :host{display:block;width:100%;margin-bottom:8px}fieldset{margin:0;padding:0;border:0;min-width:0}legend{position:absolute;width:1px;height:1px;padding:0;overflow:hidden;clip:rect(0,0,0,0);white-space:nowrap}.choices{display:flex;gap:8px;flex-wrap:wrap}label{position:relative;display:flex;align-items:center;justify-content:center;gap:8px;flex:1 1 80px;min-height:46px;padding:10px 12px;background:#fff;border:2px solid #cbd5e1;border-radius:8px;color:#25364c;font-size:13px;font-weight:650;cursor:pointer}input{position:absolute;opacity:0;width:1px;height:1px}label:has(input:focus-visible){outline:3px solid #73aaf5;outline-offset:2px}.selected{background:#eaf3ff;border-color:#1764cc;color:#154f9c}.positive{background:#dcfce7;border-color:#16a34a;color:#166534}.negative{background:#fee2e2;border-color:#dc2626;color:#991b1b}fieldset:disabled label{opacity:.6;cursor:not-allowed}.tick{font-size:15px}@media(max-width:600px){label{min-height:48px}}
 `]})
export class FleetChoiceComponent {
 @Input() value='';
 @Input() options:string[]=[];
 @Input() label='Select a result';
 @Input() disabled=false;
 @Output() valueChange=new EventEmitter<string>();
 readonly group='fleet-choice-'+(++nextChoiceId);
}
