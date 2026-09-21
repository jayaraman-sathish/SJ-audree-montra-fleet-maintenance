import {Component,OnInit} from '@angular/core';
import {CommonModule} from '@angular/common';
import {FormsModule} from '@angular/forms';
import {HttpClient,HttpHeaders} from '@angular/common/http';

@Component({
 selector:'app-ai-configuration',
 standalone:true,
 imports:[CommonModule,FormsModule],
 templateUrl:'./ai-configuration.component.html',
 styleUrl:'./ai-configuration.component.css'
})
export class AiConfigurationComponent implements OnInit {
 key=''; updatedBy='AI Administrator'; modules:any[]=[]; loading=false; saving=false; error=''; message='';
 constructor(private http:HttpClient){}
 ngOnInit(){try{this.key=sessionStorage.getItem('veerai-admin-key')||''}catch{} if(this.key)this.load();}
 headers(){return new HttpHeaders({'X-Veerai-Admin-Key':this.key});}
 load(){if(!this.key.trim()){this.error='Enter the AI Administrator key configured by your system administrator.';return}this.loading=true;this.error='';this.http.get<any[]>('/api/ai/configuration',{headers:this.headers()}).subscribe({next:x=>{this.modules=x;this.loading=false;try{sessionStorage.setItem('veerai-admin-key',this.key)}catch{}},error:e=>{this.loading=false;this.error=e.status===401?'The AI Administrator key was not accepted.':(e.error?.message||'AI configuration could not be loaded.')}});}
 save(){if(this.saving||!this.modules.length)return;this.saving=true;this.error='';this.message='';this.http.put('/api/ai/configuration',{modules:this.modules.map(x=>({code:x.code,enabled:!!x.enabled})),updatedBy:this.updatedBy},{headers:this.headers()}).subscribe({next:()=>{this.saving=false;this.message='AI read-only access configuration saved.'},error:e=>{this.saving=false;this.error=e.status===401?'The AI Administrator key was not accepted.':(e.error?.message||'AI configuration could not be saved.')}});}
}
