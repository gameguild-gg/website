'use client';

import { Button } from '@game-guild/ui';

type Props = {
  error: Error & { digest?: string };
  reset: () => void;
}

function Error({ error, reset }: Readonly<Props>) {
  return (<div>
    <Button onClick={reset}>Reset</Button>
  </div>);
}

export default Error;
