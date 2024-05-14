"use client";

import React from "react";
import { useFormStatus } from "react-dom";
import { Button } from "@/components/ui/button";

type Props = React.ButtonHTMLAttributes<HTMLButtonElement> & {
  children: React.ReactNode;
};

export function SubmitButton({ children = "Submit" }: Readonly<Props>) {
  const { pending } = useFormStatus();

  return (
    <Button type="submit" aria-disabled={pending}>
      {children}
    </Button>
  );
}