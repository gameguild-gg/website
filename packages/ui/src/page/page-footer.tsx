import React from "react";

type PageFooterProps = {
  children: React.ReactNode;
};

function PageFooter({ children }: Readonly<PageFooterProps>) {
  return (<div><p>{children}</p></div>);
}

export { PageFooter };