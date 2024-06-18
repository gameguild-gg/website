import React from 'react';
import { Metadata, ResolvingMetadata } from 'next';
import { GoogleAnalytics, GoogleTagManager } from '@next/third-parties/google';
import { SessionProvider } from 'next-auth/react';
import { Web3Provider } from '@/components/web3/web3-context';
import { TooltipProvider } from '@/components/ui/tooltip';
import { Toaster } from '@/components/ui/toaster';
import { environment } from '@/config/environment';

type Props = {
  children: React.ReactNode;
  params: {
    locale: string;
  };
};

export default async function Layout({
  children,
  params: { locale },
}: Readonly<Props>) {
  return (
    <html lang={locale}>
      <body className="p-0 m-0">
        <GoogleAnalytics gaId={environment.GoogleAnalyticsMeasurementId} />
        <GoogleTagManager gtmId={environment.GoogleTagManagerId} />
        {/*<ThemeProvider>*/}
        <SessionProvider>
          <Web3Provider>
            <TooltipProvider>{children}</TooltipProvider>
            <Toaster />
          </Web3Provider>
          {/*</ThemeProvider>*/}
        </SessionProvider>
      </body>
    </html>
  );
}

export async function generateMetadata(
  { params }: Props,
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
