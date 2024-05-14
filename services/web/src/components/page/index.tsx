import React from "react";
import { PageRoot } from "./page-root";
import { PageContent } from "./page-content";
import { PageFooter } from "@/components/page/page-footer";
import { PageHeader } from "@/components/page/page-header";

type Props = React.HTMLAttributes<HTMLDivElement> & {
  children?: React.ReactNode;
};

const Page: React.FunctionComponent<Readonly<Props>> & {
  Header: typeof PageHeader;
  Content: typeof PageContent;
  Footer: typeof PageFooter;
} = ({ children }: Readonly<Props>) => {
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
