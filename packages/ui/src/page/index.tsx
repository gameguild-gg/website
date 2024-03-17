import React from "react";
import { PageRoot } from "./page-root";
import { PageHeader } from "./page-header";
import { PageContent } from "./page-content";
import { PageFooter } from "./page-footer";

type PageProps = {
  children: React.ReactNode;
};

const Page: React.FunctionComponent<Readonly<PageProps>> & {
  Header: typeof PageHeader;
  Content: typeof PageContent;
  Footer: typeof PageFooter;
} = ({ children }: Readonly<PageProps>) => {
  return (
    <PageRoot>
      {children}
    </PageRoot>
  );
};

Page.Header = PageHeader;
Page.Content = PageContent;
Page.Footer = PageFooter;

export { Page };