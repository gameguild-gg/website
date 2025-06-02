'use client';

import React from 'react';
import { useFormState } from 'react-dom';
import { SubmitButton } from '../ui/submit-button';
import { ContactFormState, submitContactForm } from '@/lib/old/contact/actions';

const initialState: ContactFormState = {};

export default function ContactForm() {
  const [state, formAction] = useFormState(submitContactForm, initialState);

  return (
    <form action={formAction}>
      <SubmitButton>Submit</SubmitButton>
    </form>
  );
}
