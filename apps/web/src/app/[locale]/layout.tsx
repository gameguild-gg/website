import React, { ReactNode } from 'react';
import { notFound } from 'next/navigation';
import { GoogleAnalytics, GoogleTagManager } from '@next/third-parties/google';
import { hasLocale, Locale, NextIntlClientProvider } from 'next-intl';
import { setRequestLocale } from 'next-intl/server';
import { environment } from '@/config/environment';
import { routing } from '@/i18n/routing';
import { ThemeProvider } from '@/components/theme/theme-provider';
import { WebVitals } from '@/components/analytics/web-vitals';
import '@/styles/globals.css';

type Props = {
  children: ReactNode;
  params: Promise<{ locale: Locale }>;
};

export default async function Layout({ children, params }: Props): Promise<React.JSX.Element> {
  const { locale } = await params;

  if (!hasLocale(routing.locales, locale)) notFound();

  // Enable static rendering.
  setRequestLocale(locale);

  return (
    <html lang={locale} suppressHydrationWarning>
      <body>
        <WebVitals />
        <NextIntlClientProvider>
          <GoogleAnalytics gaId={environment.GoogleAnalyticsMeasurementId} />
          <GoogleTagManager gtmId={environment.GoogleTagManagerId} />
          <ThemeProvider attribute="class" defaultTheme="system" enableSystem disableTransitionOnChange>
            {/*TODO: Move this to a better place*/}
            {/*<ThemeToggle />*/}
            {children}
            {/*TODO: Move this to a better place*/}
            {/*<FeedbackFloatingButton />*/}
          </ThemeProvider>
        </NextIntlClientProvider>
      </body>
    </html>
  );
}
