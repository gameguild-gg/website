'use client';

import React from 'react';

type Props = {
  error: Error & { digest?: string };
  reset: () => void;
};

export default function GlobalError({ error, reset }: Readonly<Props>): React.JSX.Element {
  return (
    <html>
    <body>
    <p>{error.digest}</p>
    <button onClick={() => reset()}>Try again</button>
    </body>
    </html>
  );
}
