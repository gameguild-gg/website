"use client";

import React from "react";
import { Button } from "@game-guild/ui";

type Props = {
  error: Error & { digest?: string };
  reset: () => void;
}

function GlobalError({ error, reset }: Readonly<Props>) {
  return (
    <html>
      <body>
        {/*//TODO: Add a better error page*/}
        <Button onClick={() => reset()}>Try again</Button>
      </body>
    </html>
  );
}

export default GlobalError;
