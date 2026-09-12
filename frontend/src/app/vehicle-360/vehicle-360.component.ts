import { Component } from '@angular/core';
import { CommonModule } from '@angular/common';
@Component({selector:'app-vehicle-360',standalone:true,imports:[CommonModule],templateUrl:'./vehicle-360.component.html',styleUrl:'./vehicle-360.component.css'})
export class Vehicle360Component {
 activeTab='Overview';
 tabs=['Overview','Components','PM Obligations','Service History','Defects','Documents','Telematics','Warranty & Entitlements','Campaigns','Uptime History'];
 vehicle={registration:'TN12AB1234',vin:'MTT72036001234',model:'Montra ExT Truck 7T',customer:'ABC Logistics Ltd.',depot:'Chennai Depot',odometer:'48,520 km',hours:'3,120 hrs',soc:'78%',status:'Active'};
 components=[['Battery Pack','BAT-800-2026-1188','Installed'],['Traction Motor','TM-7T-00982','Installed'],['Motor Controller','MC-7T-01442','Installed'],['On-board Charger','OBC-7T-00419','Installed']];
 pm=[['PM-10K','50,000 km','1,480 km','Due Soon'],['Annual Inspection','15 Oct 2026','34 days','Planned']];
 service=[['SE-2026-01882','22 Aug 2026','PM Service','Closed'],['SE-2026-01440','04 Jul 2026','Breakdown','Closed']];
 defects=[['DF-00192','Brake pad wear','Medium','Open'],['DF-00170','Cabin lamp intermittent','Low','Deferred']];
 documents=[['Registration Certificate','31 Mar 2028','Valid'],['Insurance','10 Jan 2027','Valid'],['Fitness Certificate','18 Dec 2026','Expiring Soon']];
 campaigns=[['CAM-2026-07','Controller firmware update','Applicable','Open']];
 uptime=[['Available','01 Sep 08:00','Open','Current'],['Maintenance','31 Aug 11:20','31 Aug 17:10','5h 50m']];
 select(tab:string){this.activeTab=tab;}
}
