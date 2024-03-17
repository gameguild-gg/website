"use client";

import React from "react";
// import { useFormStatus } from 'react-dom';
import { Button } from "@game-guild/ui";

type Props = {
  children: React.ReactNode;
};

export function SubmitButton({ children = "Submit" }: Readonly<Props>) {
  // const { pending } = useFormStatus();

  return (
    <Button
      type="submit"
      // disabled={pending}
      // aria-disabled={pending}
    >
      {children}
    </Button>
  );
}