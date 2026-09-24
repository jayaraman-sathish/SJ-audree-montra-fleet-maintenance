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
 key=''; updatedBy='AI Administrator'; modules:any[]=[]; apis:any[]=[]; loading=false; saving=false; error=''; message=''; apiEditing=false; apiDraft:any={key:'',route:'',method:'GET',module:'',dependenciesText:'',dataSensitivity:'Controlled operational evidence',readOnly:true,owner:'Fleet Platform',version:'v1',enabled:true};
 readonly dependencies:Record<string,string[]> = {
  'service-events':['vehicles'],'job-cards':['vehicles','service-events'],'breakdowns':['vehicles','service-events'],
  'pm':['vehicles'],'technicians':['vehicles','service-events','job-cards'],'documents':['vehicles','service-events','job-cards']
 };
 constructor(private http:HttpClient){}
 ngOnInit(){try{this.key=sessionStorage.getItem('veerai-admin-key')||''}catch{} if(this.key)this.load();}
 headers(){return new HttpHeaders({'X-Veerai-Admin-Key':this.key});}
 dependencyNames(item:any){return (this.dependencies[item.code]||[]).map(code=>this.modules.find(x=>x.code===code)?.name||code);}
 blocked(item:any){return this.dependencyNames(item).some(name=>this.modules.some(x=>x.name===name&&!x.enabled));}
 load(){if(!this.key.trim()){this.error='Enter the AI Administrator key configured by your system administrator.';return}this.loading=true;this.error='';this.http.get<any[]>('/api/ai/configuration',{headers:this.headers()}).subscribe({next:x=>{this.modules=x;this.loadCatalog();this.loading=false;try{sessionStorage.setItem('veerai-admin-key',this.key)}catch{}},error:e=>{this.loading=false;this.error=e.status===401?'The AI Administrator key was not accepted.':(e.error?.message||'AI configuration could not be loaded.')}});}
 loadCatalog(){this.http.get<any>('/api/ai/api-catalog',{headers:this.headers()}).subscribe({next:x=>this.apis=x.apis||[],error:()=>this.apis=[]});}
 startApi(api?:any){this.apiEditing=!!api;this.apiDraft=api?{...api,_custom:!!api.custom,dependenciesText:(api.dependencies||[]).join(', ')}:{key:'',route:'',method:'GET',module:'',dependenciesText:'',dataSensitivity:'Controlled operational evidence',readOnly:true,owner:'Fleet Platform',version:'v1',enabled:true};} cancelApi(){this.apiEditing=false;} saveApi(){const d={...this.apiDraft,dependencies:(this.apiDraft.dependenciesText||'').split(',').map((x:string)=>x.trim()).filter(Boolean)};delete d.dependenciesText;const url=d._custom?'/api/ai/api-catalog/'+d.key:'/api/ai/api-catalog';delete d._custom;const request=this.apiEditing?this.http.put(url,d,{headers:this.headers()}):this.http.post(url,d,{headers:this.headers()});request.subscribe({next:()=>{this.message='VeerAI API registration saved.';this.apiEditing=false;this.loadCatalog()},error:e=>this.error=e.error?.message||'Unable to save API registration'});} 
save(){if(this.saving||!this.modules.length)return;this.saving=true;this.error='';this.message='';this.http.put('/api/ai/configuration',{modules:this.modules.map(x=>({code:x.code,enabled:!!x.enabled})),updatedBy:this.updatedBy},{headers:this.headers()}).subscribe({next:()=>{this.saving=false;this.message='AI read-only access configuration saved.'},error:e=>{this.saving=false;this.error=e.status===401?'The AI Administrator key was not accepted.':(e.error?.message||'AI configuration could not be saved.')}});}
}
