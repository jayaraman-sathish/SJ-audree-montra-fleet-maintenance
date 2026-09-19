import {vehicle,createVisit} from './flow-helpers';
describe('Veerai reasoning panel (UI fixtures are not live AI)',()=>{
 let id;
 before(()=>vehicle('Breakdown').then(v=>createVisit('Breakdown',v)).then(x=>{id=x.jobCard.id;}));
 it('reports disabled configuration honestly and leaves service records alone',()=>{
  cy.request('/api/veerai/status').its('body.available').should('eq',false);
  cy.request({method:'POST',url:`/api/job-cards/${id}/veerai/analyse`,body:{question:'Investigate'},failOnStatusCode:false}).its('status').should('eq',503);
  cy.visit(`/service-workspace/${id}`);cy.contains('button','Veerai · Analyse this job').click();
  cy.contains('Veerai is not connected.').should('be.visible');cy.get('app-veerai textarea').should('not.exist');
 });
 it('renders evidence and hypotheses safely with a simulated provider answer',()=>{
  cy.intercept('GET','/api/veerai/status',{available:true});
  cy.intercept('POST',`/api/job-cards/${id}/veerai/analyse`,{analysis:{findings:[{text:'<script>alert("not executed")</script> Complaint recorded',sources:['S1']}],possibleCauses:[{text:'Fixture hypothesis — requires testing',sources:['S1']}],missingEvidence:[],recommendedChecks:[],qcReview:[]},sources:[{id:'S1',label:'Fixture complaint',url:`/service-workspace/${id}`,detail:'Test evidence only'}],notice:'AI advisory draft. No records changed.',model:'fixture',analysedAt:new Date().toISOString()}).as('analyse');
  cy.visit(`/service-workspace/${id}`);cy.contains('button','Veerai · Analyse this job').click();
  cy.get('app-veerai input[type=password]').type('fixture-pilot-access');cy.get('app-veerai .analyse').click();cy.wait('@analyse');
  cy.contains('Possible causes — not confirmed').should('be.visible');cy.contains('Fixture hypothesis').should('be.visible');
  cy.get('app-veerai script').should('not.exist');cy.contains('button','View evidence sources').click();cy.contains('Test evidence only').scrollIntoView().should('be.visible');
  cy.get('button[aria-label="Close Veerai"]').click();cy.contains('button','Veerai · Analyse this job').click();cy.get('app-veerai input[type=password]').should('have.value','');
 });
 it('shows a provider error without claiming an analysis',()=>{
  cy.intercept('GET','/api/veerai/status',{available:true});cy.intercept('POST',`/api/job-cards/${id}/veerai/analyse`,{statusCode:502,body:{message:'Provider unavailable; no records changed.'}});
  cy.visit(`/service-workspace/${id}`);cy.contains('button','Veerai · Analyse this job').click();cy.get('app-veerai input[type=password]').type('fixture');cy.get('app-veerai .analyse').click();
  cy.contains('Provider unavailable; no records changed.').should('be.visible');cy.get('app-veerai .analyse').should('be.enabled');
 });
});
