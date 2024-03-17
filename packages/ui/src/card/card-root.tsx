import React from "react";

type CardRootProps = React.HTMLAttributes<HTMLDivElement> & {
  children: React.ReactNode;
};

export default function CardRoot({ children }: Readonly<CardRootProps>) {
  return (<div>{children}</div>);
}
