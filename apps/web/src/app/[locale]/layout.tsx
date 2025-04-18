import React, { ReactNode } from 'react';
import { hasLocale, Locale, NextIntlClientProvider } from 'next-intl';
import '@/styles/globals.css';
import { routing } from '@/i18n/routing';
import { setRequestLocale } from 'next-intl/server';
import { notFound } from 'next/navigation';
import { GoogleAnalytics, GoogleTagManager } from '@next/third-parties/google';
import { environment } from '@/config/environment';
import { ThemeToggle } from '@/components/theme/theme-toggle';
import { ThemeProvider } from '@/components/theme/theme-provider';
import { FloatingFeedbackButton } from '@/components/floating-issue-button/floating-issue-button';
import { WebVitals } from '@/components/analytics/web-vitals';

type Props = {
  children: ReactNode;
  params: Promise<{ locale: Locale }>;
};

export default async function Layout({ children, params }: Props): Promise<React.JSX.Element> {
  const { locale } = await params;

  // Ensure that the incoming `locale` is valid
  if (!hasLocale(routing.locales, locale)) notFound();

  // Enable static rendering
  setRequestLocale(locale);

  return (
    <html lang={locale} suppressHydrationWarning>
      <body>
        <WebVitals />
        <NextIntlClientProvider>
          <GoogleAnalytics gaId={environment.GoogleAnalyticsMeasurementId} />
          <GoogleTagManager gtmId={environment.GoogleTagManagerId} />
          <ThemeProvider attribute="class" defaultTheme="system" enableSystem disableTransitionOnChange>
            <ThemeToggle />
            {children}
            <FloatingFeedbackButton />
          </ThemeProvider>
        </NextIntlClientProvider>
      </body>
    </html>
  );
}
