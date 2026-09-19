import {AfterViewInit, Directive, ElementRef, OnDestroy} from '@angular/core';

/** Adds labelled mobile cards to flat record tables; complex matrices keep scrolling. */
@Directive({selector:'table[fleetGrid]', standalone:true})
export class FleetGridDirective implements AfterViewInit, OnDestroy {
  private observer?: MutationObserver;
  constructor(private element: ElementRef<HTMLTableElement>) {}
  ngAfterViewInit() {
    this.decorate();
    this.observer = new MutationObserver(() => this.decorate());
    this.observer.observe(this.element.nativeElement,{childList:true,subtree:true,characterData:true});
  }
  ngOnDestroy(){this.observer?.disconnect();}
  private decorate(){
    const table=this.element.nativeElement;
    const rows=Array.from(table.rows);
    const head=rows.find(row=>Array.from(row.cells).some(cell=>cell.tagName==='TH'));
    if(!head)return;
    const headers=Array.from(head.cells).map(cell=>cell.textContent?.trim()||'Actions');
    let simple=Array.from(head.cells).every(cell=>cell.colSpan===1&&cell.rowSpan===1);
    head.classList.add('fleet-header-row');
    Array.from(head.cells).forEach((cell,i)=>cell.classList.toggle('fleet-action-cell',/^actions?$/i.test(headers[i])));
    table.setAttribute('role','table');
    for(const row of rows){
      if(row===head)continue;
      const cells=Array.from(row.cells);
      const record=cells.length===headers.length&&cells.every(cell=>cell.colSpan===1&&cell.rowSpan===1&&cell.tagName==='TD');
      const empty=cells.length===1&&!!row.querySelector('.empty')||row.classList.contains('empty');
      if(!record&&!empty)simple=false;
      row.classList.toggle('fleet-data-row',record);
      if(!record)continue;
      row.setAttribute('role','row');
      cells.forEach((cell,i)=>{
        cell.setAttribute('data-field',headers[i]);
        cell.setAttribute('role','cell');
        const isAction=/^actions?$/i.test(headers[i]);
        cell.classList.toggle('fleet-action-cell',isAction);
        cell.classList.toggle('fleet-reference-cell',/^(event|source|job card|work order|task|no\.|reference|breakdown|vehicle|part no\.)$/i.test(headers[i]));
      });
    }
    table.classList.toggle('fleet-records',simple);
  }
}
