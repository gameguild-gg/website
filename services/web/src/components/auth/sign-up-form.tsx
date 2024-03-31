"use client";

import React from "react";
import { useFormState } from "react-dom";
import { SignUpFormState, signUpWithEmailAndPassword } from "@/lib/auth/actions";
import { SubmitButton } from "../ui/submit-button";

const initialState: SignUpFormState = {};

export default function SignUpForm() {
  const [state, formAction] = useFormState(signUpWithEmailAndPassword, initialState);

  return (
    <form action={formAction}>
      <SubmitButton>Sign Up</SubmitButton>
    </form>
  );
}
