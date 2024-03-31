"use client";

import { Button } from "@/components/ui/button";

type Props = {
  error: Error & { digest?: string };
  reset: () => void;
}

export default function Error({ error, reset }: Readonly<Props>) {
  return (<div>
    <Button onClick={reset}>Reset</Button>
  </div>);
}

