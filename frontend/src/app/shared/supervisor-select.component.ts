import {Component,Input,Output,EventEmitter,OnInit} from '@angular/core';
import {CommonModule} from '@angular/common';
import {FormsModule} from '@angular/forms';
import {HttpClient} from '@angular/common/http';
@Component({selector:'app-supervisor-select',standalone:true,imports:[CommonModule,FormsModule],template:`
<select [id]="inputId" aria-label="Assigned supervisor" [disabled]="disabled||loading" [ngModel]="value" (ngModelChange)="valueChange.emit($event)">
<option value="">{{loading?'Loading supervisors…':'Select supervisor'}}</option>
<option *ngIf="value&&!hasCurrent()" [value]="value">{{value}} (saved name)</option>
<option *ngFor="let s of rows" [value]="s.name">{{s.name}} · {{s.code}}</option></select>
<small *ngIf="error" role="alert">{{error}} <button type="button" (click)="load()">Retry</button></small>
<small *ngIf="!loading&&!error&&!rows.length">Add active supervisors in System Masters.</small>`,styles:[`:host{display:block}select{width:100%;min-height:42px;padding:8px 10px;border:1px solid #cbd5e1;border-radius:7px;background:white;color:#102440}small{display:block;color:#b45309}`]})
export class SupervisorSelectComponent implements OnInit{
 @Input() value='';@Input() disabled=false;@Input() inputId='';@Output() valueChange=new EventEmitter<string>();rows:any[]=[];loading=false;error='';
 constructor(private h:HttpClient){}ngOnInit(){this.load()}hasCurrent(){return this.rows.some(s=>s.name===this.value)}
 load(){this.loading=true;this.error='';this.h.get<any[]>('/api/supervisors').subscribe({next:x=>{this.rows=x;this.loading=false},error:()=>{this.loading=false;this.error='Unable to load supervisors.'}})}
}
