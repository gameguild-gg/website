"use client";

import React from "react";
import { useFormState } from "react-dom";
import { ContactFormState, submitContactForm } from "@/lib/contact/actions";
import { SubmitButton } from "../ui/submit-button";

const initialState: ContactFormState = {};

export default function ContactForm() {
  const [state, formAction] = useFormState(submitContactForm, initialState);

  return (
    <form action={formAction}>
      <SubmitButton>Submit</SubmitButton>
    </form>
  );
}
