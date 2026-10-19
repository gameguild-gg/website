import React from 'react';

/**
 * Compatibility route used by the authenticated entry points.  The legacy
 * public policy URL remains available while `/legal/privacy` is the consent
 * link exposed from sign-in and sign-up.
 */
export default async function Page(): Promise<React.JSX.Element> {
  return (
    <article>
      <header>
        <h1>Privacy Policy</h1>
        <h2>Our Privacy Commitment</h2>
      </header>
    </article>
  );
}
