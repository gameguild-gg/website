'use client';

import React from 'react';

type Props = {
  error: Error & { digest?: string };
  reset: () => void;
};

export default function GlobalError({ error, reset }: Readonly<Props>) {
  return (
    <html>
      <body>
        {/*//TODO: Add a better notFound layout*/}
        <button onClick={() => reset()}>Try again</button>
      </body>
    </html>
  );
}
