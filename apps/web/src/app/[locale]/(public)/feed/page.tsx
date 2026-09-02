import { redirect } from '@/i18n/navigation';
import React from 'react';

/** The authenticated social feed is the signed-in home page. */
export default async function LegacyFeedRedirectPage({
  params,
}: {
  params: Promise<{ locale: string }>;
}): Promise<never> {
  const { locale } = await params;
  redirect({ href: '/', locale });
  throw new Error('unreachable');
}
