import React from "react";
import { Page } from "@/components/page";

const LINKS = [
  { href: "/about", label: "About" },
  { href: "/blog", label: "Blog" },
  { href: "/contact", label: "Contact" }
];

type Props = {
  children: React.ReactNode;
};

export default function Layout({ children }: Readonly<Props>) {
  return (
    <Page>
      <Page.Header>
        <Page.Header.Menu links={LINKS} />
      </Page.Header>
      <Page.Content>
        {children}
      </Page.Content>
      <Page.Footer>

      </Page.Footer>
    </Page>
  );
}

