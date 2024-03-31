import React from "react";
import { Metadata, ResolvingMetadata } from "next";
import { ThemeProvider } from "@/components/theme/theme-context";
import { Web3Provider } from "@/components/web3/web3-context";

type Props = {
  children: React.ReactNode;
  modal: React.ReactNode;
  params: {
    locale: string;
  };
};

export default async function Layout({ children, modal, params: { locale } }: Readonly<Props>) {
  return (
    <html lang={locale} className="h-full">
    <body className="h-full">
    <ThemeProvider>
      <Web3Provider>
        {children}
        {modal}
        <div id="modal-root" />
      </Web3Provider>
    </ThemeProvider>
    </body>
    </html>
  );
}

export async function generateMetadata({ params }: Props, parent: ResolvingMetadata): Promise<Metadata> {
  return {
    title: {
      template: "%s | Game Guild",
      default: "Game Guild"
    },
    description: "A awesome game development community"
  };
}
