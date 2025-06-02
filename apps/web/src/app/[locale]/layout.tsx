import React, { PropsWithChildren } from 'react';
import { notFound } from 'next/navigation';
import { GoogleAnalytics, GoogleTagManager } from '@next/third-parties/google';
import { SessionProvider } from 'next-auth/react';
import { hasLocale, NextIntlClientProvider } from 'next-intl';
import { setRequestLocale } from 'next-intl/server';
import { WebVitals } from '@/components/analytics/web-vitals';
import { ThemeProvider } from '@/components/theme/theme-provider';
import { Web3Provider } from '@/components/web3/web3-context';
import { auth } from '@/auth';
import { environment } from '@/configs/environment';
import { routing } from '@/i18n/routing';
import { PropsWithLocaleParams } from '@/types';
import { Metadata } from 'next';

export async function generateMetadata({ params }: PropsWithLocaleParams): Promise<Metadata> {
  // const { locale } = await params;

  // const metadata = getWebsiteMetadata(locale);

  return {
    title: {
      template: ' %s | Matheus Martins',
      default: 'Matheus Martins',
    },
  };
}

export default async function Layout({ children, params }: PropsWithChildren<PropsWithLocaleParams>): Promise<React.JSX.Element> {
  const session = await auth();
  const { locale } = await params;

  if (!hasLocale(routing.locales, locale)) notFound();

  // Enable static rendering.
  setRequestLocale(locale);

  return (
    <html lang={locale} suppressHydrationWarning>
      <body>
        <WebVitals />
        <NextIntlClientProvider>
          <GoogleAnalytics gaId={environment.googleAnalyticsMeasurementId} />
          <GoogleTagManager gtmId={environment.googleTagManagerId} />
          <ThemeProvider attribute="class" defaultTheme="system" enableSystem disableTransitionOnChange>
            {/*TODO: see if the session has a user and the user is authenticated by web3 and if so set the account address*/}
            <Web3Provider>
              <SessionProvider session={session}>
                {/*TODO: Move this to a better place*/}
                {/*<ThemeToggle />*/}
                {children}
                {/*TODO: Move this to a better place*/}
                {/*<FeedbackFloatingButton />*/}
              </SessionProvider>
            </Web3Provider>
          </ThemeProvider>
        </NextIntlClientProvider>
      </body>
    </html>
  );
}
