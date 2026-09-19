import {vehicle,createVisit,finishChecks,qcAndRelease,unique,image} from './flow-helpers';
describe('PM, Breakdown and Maintenance complete service journeys',()=>{
 for(const kind of ['Breakdown','Maintenance','PM'])it(`${kind}: intake, allocation, checklist, QC, release and report`,()=>{
  vehicle(kind).then(v=>createVisit(kind,v).then(({jobCard:j,serviceEvent:e})=>{
   cy.request({method:'POST',url:`/api/service-events/${e.id}/release`,body:{releasedBy:'Test',remarks:'Premature'},failOnStatusCode:false}).its('status').should('eq',409);
   cy.visit(`/service-workspace/${j.id}?tab=release`);cy.contains('button','Release Vehicle').should('be.disabled');
   cy.request('/api/technicians').then(({body:techs})=>cy.request('PUT',`/api/job-cards/${j.id}/assign`,{technicianId:techs.find(t=>t.isActive).id,bay:'TEST-BAY',assignOpenTasks:true,assignedBy:'Test Supervisor'}));
   cy.request(`/api/service-workspace/${j.id}`).then(({body:w})=>expect(w.data.assignedSupervisor).to.eq('Test Supervisor'));
   cy.request('POST',`/api/work-orders/${j.id}/additional-work`,{description:'Cypress additional inspection',reason:'Verify work remains in the same visit',technicianId:null,estimatedHours:1,priority:'P3',requiresQc:true,createdBy:'Test Supervisor',createdRole:'Supervisor'}).then(({body:task})=>expect(task.jobCardId).to.eq(j.id));
   cy.request('/api/job-cards').then(({body:rows})=>expect(rows.filter(row=>row.vehicle===v.registrationNumber)).to.have.length(1));
   finishChecks(j.id);
   cy.request(`/api/job-cards/${j.id}/release-readiness`).its('body.canQc').should('eq',true);
   qcAndRelease(j.id,e.id,v.registrationNumber);
   cy.request({method:'PUT',url:`/api/job-cards/${j.id}/supervisor`,body:{supervisorName:'Changed after release'},failOnStatusCode:false}).its('status').should('eq',409);
  }));
 });
 it('shows the model picture when the variant picture is blank',()=>{
  const code=unique('PHOTO');let modelId;
  cy.request('POST','/api/pm/vehicle-models',{modelCode:code,name:'Cypress image fixture',manufacturerCode:'MONTRA',vehicleTypeCode:'TRUCK',powertrainCode:'EV',imageUrl:image,isActive:true}).then(({body:m})=>{modelId=m.id;return cy.request('POST','/api/pm/vehicle-variants',{vehicleModelMasterId:m.id,variantCode:code,name:'No variant image',imageUrl:'',configuration:'TEST',isActive:true});}).then(({body:v})=>{
   cy.visit('/pm?tab=enrollment');cy.contains('button','Enroll Vehicle').click();
   cy.get('.modal-card').contains('label','Model').find('select').select(modelId);
   cy.get('.modal-card').contains('label','Variant').find('select').select(`${code} · No variant image`);
   cy.get('.modal-card img.preview').should('have.attr','src',image);
  });
 });
});
