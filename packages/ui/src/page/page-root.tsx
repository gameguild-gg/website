import React from "react";

type PageRootProps = {
  children: React.ReactNode;
};

function PageRoot({ children }: Readonly<PageRootProps>) {
  return (<div><p>{children}</p></div>);
}

export { PageRoot };