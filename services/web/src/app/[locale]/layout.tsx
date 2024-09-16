import React, {PropsWithChildren} from 'react';
import {Metadata, ResolvingMetadata} from 'next';
import {GoogleAnalytics, GoogleTagManager} from '@next/third-parties/google';
import {SessionProvider} from 'next-auth/react';
import {Web3Provider} from '@/components/web3/web3-context';
import {TooltipProvider} from '@/components/ui/tooltip';
import {Toaster} from '@/components/ui/toaster';
import {ParamsWithLocale, PropsWithLocaleParams} from '@/types';
import {environment} from '@/config/environment';

export async function generateMetadata(
  parent: ResolvingMetadata,
): Promise<Metadata> {
  return {
    title: {
      template: '%s | Game Guild',
      default: 'Game Guild',
    },
    description: 'A awesome game development community',
  };
}

export async function generateStaticParams(): Promise<ParamsWithLocale[]> {
  return [];
}

export default async function Layout({
                                       children,
                                       params: {locale},
                                     }: Readonly<PropsWithChildren<PropsWithLocaleParams>>) {
  return (
    <html lang={locale}>
    <body className="p-0 m-0">
    <GoogleAnalytics gaId={environment.GoogleAnalyticsMeasurementId}/>
    <GoogleTagManager gtmId={environment.GoogleTagManagerId}/>
    {/*<ThemeProvider>*/}
    <SessionProvider>
      <Web3Provider>
        <TooltipProvider>{children}</TooltipProvider>
        <Toaster/>
      </Web3Provider>
      {/*</ThemeProvider>*/}
    </SessionProvider>
    </body>
    </html>
  );
}
