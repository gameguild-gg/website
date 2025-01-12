import React, { PropsWithChildren } from 'react';
import { Metadata } from 'next';
import { GoogleAnalytics, GoogleTagManager } from '@next/third-parties/google';
import { Web3Provider } from '@/components/web3/web3-context';
import { TooltipProvider } from '@/components/ui/tooltip';
import { Toaster } from '@/components/ui/toaster';
import { ParamsWithLocale, PropsWithLocaleParams } from '@/types';
import { environment } from '@/config/environment';
import { FloatingFeedbackButton } from '@/components/floating-issue-button/floating-issue-button';

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

export async function generateStaticParams(): Promise<ParamsWithLocale[]> {
  return [];
}

export default async function Layout({
  children,
  params: { locale },
}: Readonly<PropsWithChildren<PropsWithLocaleParams>>): Promise<JSX.Element> {
  return (
    <html lang={locale}>
      <body className="p-0 m-0">
        <GoogleAnalytics gaId={environment.GoogleAnalyticsMeasurementId} />
        <GoogleTagManager gtmId={environment.GoogleTagManagerId} />
        {/*<ThemeProvider>*/}
        <Web3Provider>
          <TooltipProvider>{children}</TooltipProvider>
          <FloatingFeedbackButton />
          <Toaster />
        </Web3Provider>
        {/*</ThemeProvider>*/}
      </body>
    </html>
  );
}
