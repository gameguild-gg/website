import React, { PropsWithChildren } from 'react';
import { Metadata } from 'next';
import { GoogleAnalytics, GoogleTagManager } from '@next/third-parties/google';

import { Web3Provider } from '@/components/web3/web3-context';
import { TooltipProvider } from '@/components/ui/tooltip';
import { Toaster } from '@/components/ui/toaster';
import { FloatingFeedbackButton } from '@/components/floating-issue-button/floating-issue-button';
import { WebVitals } from '@/components/analytics';
import { ThemeProvider } from '@/components/theme';
import { environment } from '@/config/environment';
import { PropsWithLocaleParams } from '@/types';

export async function generateMetadata(): Promise<Metadata> {
  const host =
    process.env.NODE_ENV === 'production' ? 'gameguild.gg' : 'localhost:3000';
  const protocol = process.env.NODE_ENV === 'production' ? 'https' : 'http';
  const baseUrl = `${protocol}://${host}`;

  return {
    title: {
      template: '%s | Game Guild',
      default: 'Game Guild',
    },
    description: 'A awesome game development community',
    openGraph: {
      type: 'website',
      locale: 'en_US',
      url: baseUrl,
      siteName: 'Game Guild',
      images: [
        {
          url: `${baseUrl}/assets/images/logo-icon.png`,
          width: 338,
          height: 121,
          alt: 'Game Guild Logo',
        },
      ],
    },
  };
}

async function Layout({ children, params: { locale } }: Readonly<PropsWithChildren<PropsWithLocaleParams>>) {
  return (
    <html lang={locale}>
      <body>
        <GoogleAnalytics gaId={environment.GoogleAnalyticsMeasurementId} />
        <GoogleTagManager gtmId={environment.GoogleTagManagerId} />
        <WebVitals />
        <ThemeProvider attribute="class" defaultTheme="system" enableSystem disableTransitionOnChange>
          <Web3Provider>
            <TooltipProvider>
              {children}
            </TooltipProvider>
            <Toaster />
            <FloatingFeedbackButton />
          </Web3Provider>
        </ThemeProvider>
      </body>
    </html>
  );
}

export default Layout;
