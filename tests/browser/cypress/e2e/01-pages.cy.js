const routes=['/','/appointments','/service','/breakdown','/maintenance-requests','/work-orders','/tasks','/pm?tab=history','/search','/vehicle','/pm-obligations','/campaigns','/pm?tab=masters','/pm-programs','/pm?tab=enrollment','/technicians','/parts','/warranty','/documents','/sla','/release','/availability','/offhire','/quality','/maintenance-analytics','/audit'];
describe('All operational pages on the isolated application',()=>{
 for(const route of routes)it(`renders ${route} without JavaScript or API server errors`,()=>{
  const errors=[];let pending=0;
  cy.intercept('/api/**',req=>{pending++;req.on('response',res=>{pending--;if(res.statusCode>=500)errors.push(`${req.method} ${req.url}: ${res.statusCode}`);});});
  cy.visit(route);
  cy.get('app-root').should('contain.text','Montra Fleet Maintenance');
  cy.get('#fleet-page-content').should('not.be.empty');
  cy.get('#fleet-page-content').find('h2,h3').should('have.length.greaterThan',0);
  cy.wrap(null).should(()=>expect(pending,'pending API requests').to.eq(0));
  cy.then(()=>expect(errors,'API server errors').to.deep.equal([]));
 });
});
