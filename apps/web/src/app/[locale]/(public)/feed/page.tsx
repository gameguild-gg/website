import { redirect } from '@/i18n/navigation';
import React from 'react';

/** The authenticated feed lives inside the dashboard shell at `/social`. */
export default async function LegacyFeedRedirectPage({
  params,
}: {
  params: Promise<{ locale: string }>;
}): Promise<never> {
  const { locale } = await params;
  redirect({ href: '/social', locale });
  throw new Error('unreachable');
}
