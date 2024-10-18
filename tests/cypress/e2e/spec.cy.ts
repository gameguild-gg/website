describe('template spec', () => {
  before(() => {
    cy.visit('http://localhost:3000');
    cy.findByText('Connect').click();
    cy.get('#email').type('gg@tuamaeaquelaursa.com');
    cy.get('button').last().click();

    // Go to the second page directly without cy.origin
    cy.visit('https://tuamaeaquelaursa.com/gg');

    // Wait for the page to load and perform actions
    cy.wait(5000); // Ensure everything has loaded

    // Click the first occurrence of 'no-reply@gameguild.gg'
    cy.findAllByText('no-reply@gameguild.gg').first().click();

    // Get the href of the 'Connect' link and visit it
    cy.findByText('Connect').click();
  });

  it('Create Project', () => {
    cy.findByText('CREATE').click();
  });
});
