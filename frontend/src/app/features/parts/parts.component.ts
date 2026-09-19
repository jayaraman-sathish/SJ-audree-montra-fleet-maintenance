import {Component,OnInit} from '@angular/core';
import {CommonModule} from '@angular/common';
import {FormsModule} from '@angular/forms';
import {HttpClient} from '@angular/common/http';
import {RouterLink} from '@angular/router';
import {FleetDateComponent} from '../../shared/fleet-date.component';
import {FleetGridDirective} from '../../shared/fleet-grid.directive';
@Component({selector:'app-parts',standalone:true,imports:[CommonModule,FormsModule,RouterLink,FleetDateComponent,FleetGridDirective],templateUrl:'./parts.component.html',styleUrl:'./parts.component.css'})
export class PartsComponent implements OnInit {
 tab='stock';masters:any[]=[];locations:any[]=[];stock:any[]=[];requests:any[]=[];tx:any[]=[];reconciliation:any[]=[];jobs:any[]=[];modal='';error='';message='';busy=false;editingId='';part:any={};draft:any={};operator='';photo:File|null=null;photoSource='';q='';applied='';location='';vehicle='';
 constructor(private h:HttpClient){} ngOnInit(){try{this.operator=localStorage.getItem('fleet-control-operator')||''}catch{}this.load();}
 get differences(){return this.reconciliation.filter(x=>x.quantityDifference!==0||x.reservationDifference!==0)}
 get ledgerDifferences(){return this.differences.filter(x=>(!this.applied||x.partNumber===this.applied)&&(!this.location||x.inventoryLocationId===this.location))}
 balance(x:any){return this.reconciliation.find(r=>r.partMasterId===x.partMasterId&&r.inventoryLocationId===x.inventoryLocationId)}
 get awaiting(){return this.requests.filter(x=>x.status==='Awaiting Stock').length}get stockValue(){return this.stock.reduce((s,x)=>s+Number(x.stockValue||0),0)}
 load(){const read=(url:string,set:(x:any[])=>void)=>this.h.get<any[]>(url).subscribe({next:set,error:e=>this.error=e.error?.message||'Unable to load inventory records.'});read('/api/parts/master',x=>this.masters=x);read('/api/parts/locations',x=>this.locations=x);read('/api/parts/stock',x=>this.stock=x);read('/api/part-requests',x=>this.requests=x);read('/api/parts/transactions',x=>this.tx=x);read('/api/parts/reconciliation',x=>this.reconciliation=x);read('/api/job-cards',x=>this.jobs=x.filter(j=>!['Completed','Closed','Cancelled'].includes(j.status)));}
 filtered(rows:any[]){return rows.filter(x=>(!this.applied||JSON.stringify(x).toLowerCase().includes(this.applied.toLowerCase()))&&(!this.vehicle||String(x.vehicle||'').toLowerCase().includes(this.vehicle.toLowerCase()))&&(!this.location||x.inventoryLocationId===this.location||x.locationCode===this.locations.find(l=>l.id===this.location)?.locationCode||x.location===this.locations.find(l=>l.id===this.location)?.locationCode));}
 switchTab(t:string){this.tab=t;this.clear();} clear(){this.q='';this.applied='';this.vehicle='';this.location=''}
 open(kind:string,x:any=null){this.modal=kind;this.error='';this.message='';this.photo=null;this.photoSource='';this.editingId=kind==='part'?(x?.id||''):'';this.part=x?{...x}:{partNumber:'',description:'',category:'',unitOfMeasure:'EA',manufacturerPartNumber:'',catalogueReference:'',standardCost:0,reorderLevel:0,reorderQuantity:0,isSerialized:false,isWarrantyReturnable:false};this.draft={id:x?.id,partMasterId:x?.partMasterId||this.masters.find(p=>!p.partNumber.startsWith('DEMO-'))?.id||'',inventoryLocationId:x?.inventoryLocationId||this.locations[0]?.id||'',quantity:kind==='count'?null:1,reference:'',jobCardId:'',operationId:crypto.randomUUID()};if(x&&['reserve','issue','consume','return'].includes(kind)){this.draft.quantity=kind==='reserve'?x.quantityRequired-x.quantityReserved:kind==='issue'?x.quantityReserved-x.quantityIssued:x.quantityIssued-x.quantityReturned-x.quantityConsumed;}}
 can(x:any,kind:string){if(['Closed','Cancelled'].includes(x.serviceStatus)||x.status==='Cancelled')return false;const held=x.quantityIssued-x.quantityReturned-x.quantityConsumed;if(kind==='reserve')return x.quantityRequired>x.quantityReserved;if(kind==='issue')return x.quantityReserved>x.quantityIssued;if(kind==='return'||kind==='consume')return held>0;return held===0&&(x.quantityReserved>x.quantityIssued||x.quantityRequired>x.quantityIssued);}
 history(x:any){this.tab='ledger';this.q=this.applied=x.partNumber;this.vehicle='';this.location=''}
 pick(e:Event){this.photo=(e.target as HTMLInputElement).files?.[0]||null}
 save(){if(this.busy)return;if(this.modal==='count'&&(this.draft.quantity===null||this.draft.quantity===''||!Number.isFinite(Number(this.draft.quantity))||Number(this.draft.quantity)<0)){this.error='Enter the actual physically counted quantity, including zero if none is present.';return}if(!this.operator.trim()){this.error='Enter the operator name.';return}try{localStorage.setItem('fleet-control-operator',this.operator)}catch{}let url='',body:any,method='post';const d=this.draft,user=this.operator.trim();this.error='';
 if(this.modal==='part'){url='/api/parts/master'+(this.editingId?'/'+this.editingId:'');method=this.editingId?'put':'post';body=this.part;}
 else if(this.modal==='photo'){if(!this.photo||!this.photoSource.trim()){this.error='Choose an actual part photo and enter its catalogue/source reference.';return}url='/api/parts/master/'+d.id+'/photo';const f=new FormData();f.append('file',this.photo);f.append('source',this.photoSource);f.append('user',user);body=f;}
 else if(this.modal==='request'){if(!d.jobCardId){this.error='Select the vehicle Job Card.';return}url='/api/part-requests';body={...d,quantity:Number(d.quantity),requestedBy:user,warrantyCandidate:false,failedPartDisposition:'',workItemId:null};}
 else if(this.modal==='receive'||this.modal==='count'){if(!d.reference.trim()){this.error='Enter the receipt or physical count reference.';return}url='/api/parts/'+this.modal;body={...d,quantity:Number(d.quantity),user};}
 else{url='/api/part-requests/'+d.id+'/'+this.modal;body={quantity:this.modal==='cancel'?0:Number(d.quantity),user,reference:d.reference,operationId:d.operationId};}
 this.busy=true;this.h.request(method,url,{body}).subscribe({next:()=>{this.busy=false;this.modal='';this.message='Saved. Inventory records refreshed.';this.load()},error:e=>{this.busy=false;this.error=e.error?.message||'Unable to save. Refresh records before retrying.'}});
 }
}
