import React from "react";
import { Metadata } from "next";
import "@/styles/globals.css";

import { ThemeProvider } from "@game-guild/ui";
import { Web3Provider } from "@/components/web3/web3-context";
import CookieConsent from "@/components/cookie/cookie-consent";

type RootLayoutProps = {
  children: React.ReactNode;
  modal: React.ReactNode;
  params: {
    locale: string;
  };
};

async function RootLayout({ children, modal, params: { locale } }: Readonly<RootLayoutProps>) {
  return (
    <html lang={locale}>
    <body>
    <ThemeProvider>
      <Web3Provider>
        {children}
        {modal}
        <CookieConsent />
        {/*<div id="modal-root" />*/}
      </Web3Provider>
    </ThemeProvider>
    </body>
    </html>
  );
}

const metadata: Metadata = {
  title: {
    template: "%s | Game Guild",
    default: "Game Guild"
  },
  description: "A awesome game development community"
};

export { metadata };

export default RootLayout;
