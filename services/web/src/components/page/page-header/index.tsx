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
      <div className="max-w-7xl mx-auto px-4 sm:px-6 lg:px-8">
        <div className="flex justify-between h-16">
          <div className="flex">
            <div className="-ml-2 mr-6 flex items-center">
              <img
                alt="Logo"
                className="h-8 w-8"
                height="32"
                src="/placeholder.svg"
                style={{
                  aspectRatio: "32/32",
                  objectFit: "cover",
                }}
                width="32"
              />
            </div>
            
          </div>
        </div>
      </div>
      {children}
    </PageHeaderRoot>
  );
};

PageHeader.Menu = PageHeaderMenu;

export { PageHeader };
