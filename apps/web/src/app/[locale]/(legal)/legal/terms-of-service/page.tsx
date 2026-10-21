import React from 'react';

/**
 * Compatibility route used by the authenticated entry points.  The canonical
 * policy page is also available at `/terms-of-service`; keeping this route
 * prevents consent links from becoming published 404s.
 */
export default async function Page(): Promise<React.JSX.Element> {
  return (
    <article>
      <h1>Terms of Service</h1>
    </article>
  );
}
