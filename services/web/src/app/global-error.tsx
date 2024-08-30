'use client' // Error boundaries must be Client Components

import React from 'react';

type Props = {
  error: Error & { digest?: string };
  reset: () => void;
};

export default function GlobalError({error, reset}: Readonly<Props>) {
  return (
    // global-error must include html and body tags
    // TODO: Refactor this to use a better GlobalError layout
    <html>
    <body>
    <button onClick={() => reset()}>Try again</button>
    </body>
    </html>
  );
}
