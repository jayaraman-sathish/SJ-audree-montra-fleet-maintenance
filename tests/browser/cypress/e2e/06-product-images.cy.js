import {vehicle} from './flow-helpers';
describe('Shared product reference images',()=>{
 it('shows images grouped by classification with distinct Rhino variants',()=>{
  cy.request('/api/pm/vehicle-models').then(({body:models})=>{
   for(const code of ['SUPER_AUTO','SUPER_CARGO','EVIATOR','RHINO_5538_EV','TRACTOR_E27','TRACTOR_E45'])expect(models.find(m=>m.modelCode===code).imageUrl).to.include('https://cdn.prod.website-files.com/');
  });
  cy.request('/api/pm/vehicle-variants').then(({body:variants})=>expect(variants.find(v=>v.variantCode==='RH5538-4X2').imageUrl).not.to.eq(variants.find(v=>v.variantCode==='RH5538-6X4').imageUrl));
  cy.visit('/pm?tab=masters&focus=fleet');cy.contains('button','Vehicle Classification').click();
  cy.get('.product-reference-gallery').should('contain','Super Auto').and('contain','Super Cargo').and('contain','EVIATOR').and('contain','Manufacturer silhouette');
  cy.get('.product-reference-gallery img').should('have.length.greaterThan',6);
 });
 it('inherits shared images in enrollment, vehicle lists and Vehicle 360 without storing a copy',()=>{
  vehicle('Breakdown').then(v=>{
   expect(v.imageUrl).to.eq('');
   cy.request('/api/pm/vehicle-models').then(({body:models})=>{
    const expected=models.find(m=>m.id===v.modelMasterId).imageUrl;expect(expected).not.to.eq('');
    cy.request('/api/pm/enrollments').its('body').should(rows=>expect(rows.find(x=>x.id===v.id).imageUrl).to.eq(expected));
    cy.request('/api/vehicles').its('body').should(rows=>expect(rows.find(x=>x.id===v.id).imageUrl).to.eq(expected));
    cy.request(`/api/vehicle360/${v.id}`).its('body.imageUrl').should('eq',expected);
   });
  });
 });
});
