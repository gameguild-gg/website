import React from 'react';
import { MfaChallengeForm } from '@/components/auth/mfa-challenge-form';
import { resolveAllowedAuthRedirect } from '@/lib/auth/cross-domain-auth';

/**
 * Dedicated MFA challenge route. The pending challenge (mfaToken,
 * availableMethods) lives in the app-wide SessionProvider client context, so
 * the form itself decides between the challenge and a no-challenge notice;
 * the (auth) layout bounces authenticated users away.
 */
export default async function Page({
  searchParams,
}: PageProps<'/[locale]/sign-in/2fa'>): Promise<React.JSX.Element> {
  const query = await searchParams;
  const redirectTo = resolveAllowedAuthRedirect(
    query?.redirectTo ?? query?.callbackUrl,
  );

  return <MfaChallengeForm redirectTo={redirectTo} />;
}
