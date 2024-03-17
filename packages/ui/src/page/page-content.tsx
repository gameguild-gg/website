import React from "react";

type PageContentProps = {
  children: React.ReactNode;
};

function PageContent({ children }: Readonly<PageContentProps>) {
  return (<div><p>{children}</p></div>);
}

export { PageContent };