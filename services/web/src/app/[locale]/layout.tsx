import React, { PropsWithChildren } from 'react';
import { Metadata } from 'next';
import { GoogleAnalytics, GoogleTagManager } from '@next/third-parties/google';
import { Web3Provider } from '@/components/web3/web3-context';
import { TooltipProvider } from '@/components/ui/tooltip';
import { Toaster } from '@/components/ui/toaster';
import { ParamsWithLocale, PropsWithLocaleParams } from '@/types';
import { environment } from '@/config/environment';

export async function generateMetadata(): Promise<Metadata> {
  return {
    title: {
      template: '%s | Game Guild',
      default: 'Game Guild',
    },
    description: 'A awesome game development community',
    openGraph: {
      type: 'website',
      locale: 'en_US',
      url: 'https://gameguild.gg',
      siteName: 'Game Guild',
      images: [
        {
          url: './assets/images/logo-icon.png',
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
          <Toaster />
        </Web3Provider>
        {/*</ThemeProvider>*/}
      </body>
    </html>
  );
}
