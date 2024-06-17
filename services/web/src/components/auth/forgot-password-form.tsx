"use client";

import React from "react";
import { useFormState } from "react-dom";
// import { forgotPassword, ForgotPasswordFormState } from "@/lib/auth";
import { SubmitButton } from "../ui/submit-button";

// const initialState: ForgotPasswordFormState = {};

export default function ForgotPasswordForm() {
  // const [state, formAction] = useFormState(forgotPassword, initialState);

  return (
    // <form action={formAction}>
      <SubmitButton>Submit</SubmitButton>
    // </form>
  );
}
