'use client';

import React from 'react';
import { useFormState } from 'react-dom';
import { SignUpFormState, signUpWithEmailAndPassword } from '@/lib/actions/auth';
import { SubmitButton } from '../common/submit-button';

const initialState: SignUpFormState = {};

export default function SignUpForm() {
  const [state, formAction] = useFormState(signUpWithEmailAndPassword, initialState);

  return (
    <form action={formAction}>
      <SubmitButton>Sign Up</SubmitButton>
    </form>
  );
}
