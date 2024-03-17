import React from "react";

type PageHeaderProps = {
  children: React.ReactNode;
};

function PageHeader({ children }: Readonly<PageHeaderProps>) {
  return (<div><p>{children}</p></div>);
}

export { PageHeader };