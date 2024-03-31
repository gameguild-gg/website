"use client";

import React from "react";
import { useFormState } from "react-dom";
import { SignInFormState, signInWithEmailAndPassword } from "@/lib/auth/actions";
import { SubmitButton } from "../ui/submit-button";

const initialState: SignInFormState = {};

export default function SignInForm() {
  const [state, formAction] = useFormState(signInWithEmailAndPassword, initialState);

  return (
    <form action={formAction}>
      <SubmitButton>Sign In</SubmitButton>
    </form>
  );
}
