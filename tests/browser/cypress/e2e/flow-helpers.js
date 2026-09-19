const image='data:image/png;base64,iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mP8/x8AAwMCAO+j1ioAAAAASUVORK5CYII=';
const unique=prefix=>`${prefix}-${Date.now()}-${Math.floor(Math.random()*100000)}`;
function vehicle(kind){
 return cy.request('/api/pm/programs').then(({body:programs})=>cy.request('/api/pm/vehicle-models').then(({body:models})=>{
  const program=programs.find(p=>p.isActive&&p.vehicleModelMasterId&&!p.vehicleVariantMasterId);
  const model=kind==='PM'?models.find(m=>m.id===program?.vehicleModelMasterId):models[0];
  expect(model,'configured model').to.exist;
  const payload={vin:unique('TESTVIN'),registrationNumber:unique('E2E'),modelMasterId:model.id,variantMasterId:null,imageUrl:'',motorNumber:'TEST',purchaseCost:0,odometerKm:1000,operatingHours:20,energyKwh:50,batterySoc:80,maintenanceProgramId:kind==='PM'?program.id:null,createdBy:'Cypress Test'};
  for(const k of ['invoiceNumber','dealerName','insuranceNumber','depotCode','serviceCentreCode','customerCode','ownershipTypeCode','remarks'])payload[k]='TEST';
  return cy.request('POST','/api/pm/enroll',payload).its('body');
 }));
}
function createVisit(kind,v){
 if(kind==='Breakdown')return cy.request('POST','/api/breakdowns/report-and-assign',{requestId:crypto.randomUUID(),vehicleId:v.id,priority:'P2',location:'Isolated test workshop',complaint:'Cypress diagnostic flow',dispatchMode:'Workshop',assignedSupervisor:'Test Supervisor'}).its('body');
 if(kind==='Maintenance')return cy.request('POST','/api/maintenance-requests',{vehicleId:v.id,sourceType:'Workshop',sourceReference:'E2E',requestType:'Corrective',complaintCategoryCode:'GENERAL',symptomCode:'OTHER',priority:'P2',description:'Cypress maintenance flow',requestedBy:'Test Advisor',assignedSupervisor:'Test Supervisor'}).then(()=>cy.request('/api/job-cards')).then(({body:rows})=>{
  const j=rows.find(j=>j.vehicleId===v.id||j.vehicle===v.registrationNumber);expect(j,'created maintenance job').to.exist;
  return cy.request(`/api/service-workspace/${j.id}`).then(({body:w})=>({jobCard:{id:j.id,jobCardNumber:j.jobCardNumber},serviceEvent:{id:w.data.serviceEventId}}));
 });
 return cy.request('/api/pm/due-board').then(({body:rows})=>{
  const o=rows.find(x=>x.vehicleId===v.id);expect(o,'generated PM obligation').to.exist;
  return cy.request('POST','/api/appointments',{vehicleId:v.id,pmObligationId:o.id,sourceType:'PM',sourceReference:o.id,startAt:new Date(Date.now()+86400000).toISOString(),serviceCentre:'TEST',bay:unique('TESTBAY'),technicianId:null,appointmentType:'PM',priority:'P3',reason:'Cypress PM',plannedHours:1,createdBy:'Test Advisor'});
 }).then(({body:a})=>cy.request('POST',`/api/appointments/${a.id}/start-service`,{assignedSupervisor:'Test Supervisor',requestIds:[],odometerKm:1000,operatingHours:20,energyKwh:50,complaint:'',remarks:'Cypress PM arrival'})).its('body');
}
function finishChecks(jobId){
 return cy.request(`/api/tasks?jobCardId=${jobId}`).then(({body:tasks})=>{
  expect(tasks.length,'generated executable work').to.be.greaterThan(0);
  return cy.wrap(tasks).each(t=>cy.request({url:`/api/tasks/${t.id}/paper-form`,failOnStatusCode:false}).then(({status,body:form})=>{
   if(status===404)return cy.request('PUT',`/api/tasks/${t.id}/status`,{status:'Completed',actualHours:1,completionRemarks:'Completed by isolated automated test',evidenceReference:'E2E'});
   expect(status).to.eq(200);
   return cy.wrap(form.fields).each(f=>{
    let value='Test observation';
    if(f.fieldType==='OK/Not OK')value='OK';else if(f.fieldType==='Yes/No')value='No';else if(f.fieldType==='Number')value=String(f.minValue??1);else if(f.options)value=f.options.split(';')[0];
    return cy.request('PUT',`/api/tasks/${t.id}/paper-form/${f.id}`,{value,result:'Pass',remarks:'Isolated test result',evidenceReference:'E2E',executedBy:'Test Technician'});
   }).then(()=>cy.request('POST',`/api/tasks/${t.id}/paper-form/complete`,{}));
  }));
 });
}
function qcAndRelease(jobId,eventId,registration){
 cy.visit(`/service-workspace/${jobId}?tab=release`);
 cy.contains('label','QC inspector').find('input').clear().type('Cypress Inspector');
 cy.get('app-fleet-choice[label="QC result"]').contains('label','Pass').click();
 cy.get('app-fleet-choice[label="Road test required"]').contains('label','No').click();
 cy.contains('label','QC remarks').find('textarea').type('Automated test inspection');
 cy.intercept('POST',`/api/job-cards/${jobId}/qc`).as('qc');
 cy.contains('button','Save QC Result').should('be.enabled').click();cy.wait('@qc').its('response.statusCode').should('eq',200);
 cy.contains('Saved QC: Pass').should('be.visible');
 cy.intercept('POST',`/api/service-events/${eventId}/release`).as('release');
 cy.contains('button','Release Vehicle').should('be.enabled').click();cy.wait('@release').its('response.statusCode').should('eq',200);
 cy.contains('Vehicle released. This service is closed.').should('be.visible');
 cy.request(`/api/job-cards/${jobId}/report`).then(r=>{expect(r.status).to.eq(200);expect(r.body).to.include(registration).and.include('Cypress Inspector');});
 cy.request('/api/work-orders').then(({body:rows})=>expect(rows.find(j=>j.id===jobId).status).to.eq('Completed'));
 cy.request('/api/vehicles').then(({body:rows})=>expect(rows.find(v=>v.registrationNumber===registration).status).to.eq('Available'));
}

export {vehicle,createVisit,finishChecks,qcAndRelease,unique,image};
