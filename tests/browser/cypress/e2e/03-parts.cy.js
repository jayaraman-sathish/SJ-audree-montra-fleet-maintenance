describe('Inventory receipt through vehicle consumption on real test APIs',()=>{
 it('tallies reserve, partial issue, consume, return, close and ledger',()=>{
  const number=`TEST-PART-${Date.now()}`;let part,location,job,request;
  cy.request('POST','/api/parts/master',{partNumber:number,description:'Automated test fixture (not OEM stock)',category:'TEST',unitOfMeasure:'EA',manufacturerPartNumber:'TEST-ONLY',catalogueReference:'Isolated Cypress fixture',standardCost:100,reorderLevel:1,reorderQuantity:2,isSerialized:false,isWarrantyReturnable:false}).then(r=>part=r.body);
  cy.request('/api/parts/locations').then(r=>location=r.body[0]);
  cy.request('/api/job-cards').then(({body:rows})=>{job=rows.find(j=>!['Completed','Closed','Cancelled'].includes(j.status));expect(job,'open seeded job for inventory test').to.exist;});
  cy.visit('/parts');cy.contains('button','Receive Stock').click();
  cy.get('.editor').contains('label','Operator').find('input').clear().type('Cypress Stores');
  cy.then(()=>cy.get('.editor').contains('label','Part number').find('select').select(part.id));
  cy.then(()=>cy.get('.editor').contains('label','Location').find('select').select(location.id));
  cy.get('.editor').contains('label','Quantity').find('input').clear().type('10');
  cy.get('.editor').contains('label','Reference').find('input').type('TEST-RECEIPT-10');
  cy.intercept('POST','/api/parts/receive').as('receipt');cy.get('.editor').contains('button','Save').click();cy.wait('@receipt').its('response.statusCode').should('eq',200);
  cy.then(()=>cy.request('POST','/api/part-requests',{jobCardId:job.id,workItemId:null,partMasterId:part.id,inventoryLocationId:location.id,quantity:6,warrantyCandidate:false,failedPartDisposition:'',requestedBy:'Cypress Technician'})).then(r=>request=r.body);
  for(const [action,quantity] of [['reserve',6],['issue',4],['consume',3],['return',1],['cancel',0]]){
   cy.then(()=>cy.request('POST',`/api/part-requests/${request.id}/${action}`,{quantity,user:'Cypress Stores',reference:'Isolated test movement',operationId:crypto.randomUUID()}));
  }
  cy.request('/api/parts/stock').then(({body:rows})=>{const s=rows.find(s=>s.partMasterId===part.id&&s.inventoryLocationId===location.id);expect(s.onHandQty).to.eq(7);expect(s.reservedQty).to.eq(0);expect(s.availableQty).to.eq(7);});
  cy.request('/api/parts/reconciliation').then(({body:rows})=>{const s=rows.find(s=>s.partMasterId===part.id);expect(s.ledgerQty).to.eq(7);expect(s.quantityDifference).to.eq(0);expect(s.reservationDifference).to.eq(0);expect(s.consumed).to.eq(3);expect(s.atTechnician).to.eq(0);});
  cy.request('/api/parts/transactions').then(({body:rows})=>{const moves=rows.filter(s=>s.partMasterId===part.id);expect(moves).to.have.length(6);const issue=moves.find(s=>s.transactionType==='Issue');expect(issue.jobCardId).to.eq(job.id);expect(issue.vehicle).to.be.a('string').and.not.be.empty;});
  cy.visit('/parts');cy.contains('button','Ledger').click();cy.contains('td',number).should('be.visible');cy.contains('td','TEST-RECEIPT-10').should('be.visible');
  cy.then(()=>cy.request({method:'POST',url:`/api/part-requests/${request.id}/consume`,body:{quantity:1,user:'Cypress',reference:'Excess consumption',operationId:crypto.randomUUID()},failOnStatusCode:false})).its('status').should('eq',409);
 });
});
