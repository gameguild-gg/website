import React from "react";

type CardFooterProps = React.HTMLAttributes<HTMLDivElement> & {
  children: React.ReactNode;
};

export default function CardFooter({ children }: Readonly<CardFooterProps>) {
  return (<h1>{children}</h1>);
}
