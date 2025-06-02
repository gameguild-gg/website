import React from 'react';
import { SignInForm } from '@/components/auth/sign-in-form';

export default async function SignInPage(): Promise<React.JSX.Element> {
  return <SignInForm />;
}
