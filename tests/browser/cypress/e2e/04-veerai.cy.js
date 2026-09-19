import {vehicle,createVisit} from './flow-helpers';
describe('Veerai floating chat (provider replies are fixtures)',()=>{
 let id;
 before(()=>vehicle('Breakdown').then(v=>createVisit('Breakdown',v)).then(x=>{id=x.jobCard.id;}));
 function open(){cy.get('[aria-label="Open Veerai"]').click();}
 function send(text){cy.get('#veer-message').type(text);cy.get('[aria-label="Send message"]').click();}
 it('reports missing server configuration honestly',()=>{
  cy.request('/api/veerai/chat/status').its('body.available').should('eq',false);
  cy.visit('/');open();send('Montra not charging');cy.contains('Veerai is not connected.').should('be.visible');
 });
 it('uses one message box without job selectors or access key',()=>{
  cy.intercept('POST','/api/veerai/chat',{reply:'General guidance: inspect reported symptoms.',sources:[],choices:[],context:'General Montra guidance'}).as('chat');
  cy.visit('/');open();cy.get('app-veerai select,app-veerai input[type=password]').should('not.exist');
  send('Why is my Montra not charging?');cy.wait('@chat').its('request.body.message').should('include','Montra');cy.contains('General guidance:').should('be.visible');
  send('What should I check next?');cy.wait('@chat').its('request.body.history').should('have.length',2);
  cy.get('[aria-label="Minimise Veerai"]').click();open();cy.contains('What should I check next?').should('be.visible');
 });
 it('uses current workspace and safely renders evidence',()=>{
  cy.intercept('POST','/api/veerai/chat',{reply:'<script>not executed</script> Check evidence.',jobId:id,sources:[{id:'S1',label:'Complaint',url:`/service-workspace/${id}`,detail:'Fixture evidence'}],choices:[]}).as('workspace');
  cy.visit(`/service-workspace/${id}`);open();send('What could explain this complaint?');cy.wait('@workspace').its('request.body.jobId').should('eq',id);
  cy.get('app-veerai script').should('not.exist');cy.contains('summary','Evidence used').click();cy.contains('Fixture evidence').should('be.visible');
 });
 it('offers visit choices only when clarification is needed',()=>{
  let count=0;cy.intercept('POST','/api/veerai/chat',req=>{count++;req.reply(count===1?{reply:'Which service visit do you mean?',choices:[{id,label:'TS09DC2002 · latest visit'}],sources:[]}:{reply:'Selected visit analysed',jobId:id,choices:[],sources:[]});}).as('choice');
  cy.visit('/');open();send('Why is TS09DC2002 failing?');cy.wait('@choice');cy.contains('button','TS09DC2002 · latest visit').click();cy.wait('@choice').its('request.body.selectedJobId').should('eq',id);cy.contains('Selected visit analysed').should('be.visible');
 });
 it('shows no match and useful provider errors',()=>{
  cy.intercept('POST','/api/veerai/chat',{reply:'No match found. I can help with Montra vehicle service and maintenance.',sources:[],choices:[]});cy.visit('/');open();send('Write a film review');cy.contains('No match found.').should('be.visible');
  cy.intercept('POST','/api/veerai/chat',{statusCode:502,body:{message:'AI provider quota or rate limit reached.'}});send('Montra charging issue');cy.contains('AI provider quota').should('be.visible');cy.contains('button','Retry').should('be.visible');
 });
 it('floats, moves, minimises and fits mobile screens',()=>{
  cy.visit('/');open();cy.get('app-veerai .chat').then($e=>{expect($e[0].getBoundingClientRect().width).to.be.lessThan(500);});
  cy.get('app-veerai header').trigger('pointerdown',{clientX:900,clientY:200});cy.document().trigger('pointermove',{clientX:800,clientY:240}).trigger('pointerup');cy.get('app-veerai .chat').should('have.class','moved');
  cy.viewport(390,844);cy.get('app-veerai .chat').then($e=>{const r=$e[0].getBoundingClientRect();expect(r.left).to.be.at.least(0);expect(r.right).to.be.at.most(390);});cy.get('[aria-label="Minimise Veerai"]').click();cy.get('[aria-label="Open Veerai"]').should('be.visible');
 });
 it('unlocks once and retries the pending question without exposing the key in chat',()=>{
  let unlocked=false;
  cy.intercept('POST','/api/veerai/chat',req=>req.reply(unlocked?{reply:'Session is ready',sources:[],choices:[]}:{statusCode:401,body:{message:'Unlock Veerai once for this browser session.'}}));
  cy.intercept('POST','/api/veerai/chat/session',req=>{unlocked=true;req.reply({unlocked:true});});
  cy.visit('/');open();send('Montra charging issue');cy.get('app-veerai input[type=password]').type('fixture-key');cy.contains('button','Unlock').click();cy.contains('Session is ready').should('be.visible');cy.get('app-veerai input[type=password]').should('not.exist');cy.get('.messages').should('not.contain','fixture-key');
 });

});
