describe('Customer and manufacturer documents',()=>{
 it('uploads a model manual without a registration, persists metadata and downloads the original file',()=>{
  cy.request('/api/pm/vehicle-models').then(({body:models})=>{
   const model=models.find(m=>m.isActive&&m.vehicleTypeCode&&m.manufacturerCode);expect(model).to.exist;
   const title='Cypress manual '+Date.now();
   cy.visit('/documents');cy.contains('button','Manufacturer Documents').click();cy.contains('button','Upload Document').click();
   cy.get('.editor').within(()=>{
    cy.contains('label','Operator / inspector').find('input').type('Cypress uploader');
    cy.contains('label','Vehicle type').find('select').select(model.vehicleTypeCode);
    cy.contains('label','Vehicle model').find('select').select(model.id);
    cy.contains('label','Document title').find('input').type(title);
    cy.contains('label','Revision').find('input').type('R1');
    cy.contains('label','Document type').find('select').select('Service Manual');
    cy.contains('label','Job Card').should('not.exist');
    cy.get('input[type=file]').selectFile({contents:Cypress.Buffer.from('%PDF-1.4\n% isolated upload fixture\n%%EOF'),fileName:'manual.pdf',mimeType:'application/pdf'});
    cy.contains('button','Save').click();
   });
   cy.contains('Saved successfully.').should('be.visible');cy.contains('td',title).should('be.visible');
   cy.request('/api/documents').then(({body:docs})=>{
    const d=docs.find(d=>d.title===title);expect(d.vehicleId).to.eq(null);expect(d.documentScope).to.eq('Manufacturer');expect(d.modelName).to.eq(model.name);expect(d.revision).to.eq('R1');
    cy.request(d.fileUrl).its('body').should('include','isolated upload fixture');
   });
   cy.reload();cy.contains('button','Manufacturer Documents').click();cy.contains('td',title).should('be.visible');
   cy.contains('button','Customer Documents').click();cy.contains('td',title).should('not.exist');
   cy.contains('button','Upload Document').click();cy.get('.editor').contains('label','Vehicle').should('be.visible');
  });
 });
});
describe('Document upload validation',()=>{
 function upload(fields){return cy.window().then(w=>{
  const form=new w.FormData();form.append('file',new w.Blob(['%PDF-1.4\n%%EOF'],{type:'application/pdf'}),'fixture.pdf');
  Object.entries({user:'Cypress uploader',documentType:'Service Evidence',...fields}).forEach(([k,v])=>form.append(k,v));
  return w.fetch('/api/documents/upload',{method:'POST',body:form}).then(async r=>({status:r.status,body:await r.json()}));
 });}
 it('rejects missing customer vehicle and invalid model without saving a document',()=>{
  cy.visit('/documents');
  upload({documentScope:'Customer'}).its('status').should('eq',400);
  upload({documentScope:'Manufacturer',vehicleModelMasterId:crypto.randomUUID(),documentType:'Owner Manual',title:'Invalid model'}).its('status').should('eq',400);
  cy.request('/api/documents').its('body').should(d=>expect(d.some(x=>x.title==='Invalid model')).to.eq(false));
 });
 it('keeps customer files linked to their selected vehicle',()=>{
  cy.request('/api/vehicles').then(({body:vehicles})=>{
   cy.visit('/documents');upload({documentScope:'Customer',vehicleId:vehicles[0].id}).then(({status,body})=>{
    expect(status).to.eq(200);cy.request('/api/documents').its('body').should(d=>{
     const saved=d.find(x=>x.id===body.id);expect(saved.vehicleId).to.eq(vehicles[0].id);expect(saved.documentScope).to.eq('Customer');
    });
   });
  });
 });
});
