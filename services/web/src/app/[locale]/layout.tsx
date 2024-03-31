import React from "react";
import { Metadata, ResolvingMetadata } from "next";
import { ThemeProvider } from "@/components/theme/theme-context";
import { Web3Provider } from "@/components/web3/web3-context";
import { TooltipProvider } from "@/components/ui/tooltip";

type Props = {
  children: React.ReactNode;
  params: {
    locale: string;
  };
};

export default async function Layout({ children, params: { locale } }: Readonly<Props>) {
  return (
    <html lang={locale}>
    <body>
    <ThemeProvider>
      <Web3Provider>
        <TooltipProvider>
          {children}
        </TooltipProvider>
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
