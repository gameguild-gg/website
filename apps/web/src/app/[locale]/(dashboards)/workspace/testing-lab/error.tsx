'use client';

import { Button } from '@game-guild/ui/components/button';
import { AlertTriangle, RotateCcw } from 'lucide-react';
import { useEffect } from 'react';

export default function TestingLabError({ error, reset }: { error: Error & { digest?: string }; reset: () => void }) {
  useEffect(() => {
    console.error('Testing Lab route failed', error);
  }, [error]);

  return (
    <div className="flex min-h-[55vh] items-center justify-center p-6">
      <div className="max-w-lg rounded-md border p-6 text-center">
        <AlertTriangle className="mx-auto size-8 text-destructive" />
        <h1 className="mt-4 text-xl font-semibold">Testing Lab could not be loaded</h1>
        <p className="mt-2 text-sm text-muted-foreground">
          The operation failed before the page could finish loading. Retry without losing your current route.
        </p>
        <Button className="mt-5" onClick={reset}>
          <RotateCcw className="mr-2 size-4" />
          Retry
        </Button>
      </div>
    </div>
  );
}
