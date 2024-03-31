import React from "react";
import { PageHeaderRoot } from "@/components/page/page-header/page-header-root";
import { PageHeaderMenu } from "@/components/page/page-header/page-header-menu";

type Props = React.HTMLAttributes<HTMLDivElement> & {
  children?: React.ReactNode;
};

const PageHeader: React.FunctionComponent<Readonly<Props>> & {
  Menu: typeof PageHeaderMenu;
} = ({ children }: Readonly<Props>) => {
  return (
    <PageHeaderRoot>
      {children}
    </PageHeaderRoot>
  );
};

PageHeader.Menu = PageHeaderMenu;

export { PageHeader };
