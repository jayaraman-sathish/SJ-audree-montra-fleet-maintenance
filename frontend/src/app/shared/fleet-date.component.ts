import { Component, Input } from '@angular/core';
import { CommonModule } from '@angular/common';

@Component({
 selector:'app-fleet-date', standalone:true, imports:[CommonModule],
 template:`<span *ngIf="value;else missing" class="date-value"><time>{{value | date:'dd MMM yyyy':(dateOnly?'UTC':undefined)}}</time><small *ngIf="!dateOnly">{{value | date:'HH:mm'}}</small></span><ng-template #missing><span aria-label="Not recorded">—</span></ng-template>`,
 styles:[`:host{display:inline-block;min-width:100px;vertical-align:top}.date-value{display:flex;flex-direction:column;gap:4px;white-space:nowrap;font-variant-numeric:tabular-nums}time{font-weight:500;color:#14213d}small{font-size:12px;color:#52647c;font-weight:400}`]
})
export class FleetDateComponent {
 @Input() value: string | number | Date | null | undefined;
 @Input() dateOnly=false;
}
