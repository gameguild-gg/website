"use client";

import React from "react";
import { useFormStatus } from "react-dom";

type Props = {
  children: React.ReactNode;
};

export function SubmitButton({ children = "Submit" }: Readonly<Props>) {
  const { pending } = useFormStatus();

  return (
    <button type="submit" aria-disabled={pending}>
      {children}
    </button>
  );
}