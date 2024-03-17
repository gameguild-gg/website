import React from "react";

type Props = React.HTMLAttributes<HTMLDivElement> & {
  children: React.ReactNode;
};

export default function CardHeader({ children }: Readonly<Props>) {
  return (<h1>{children}</h1>);
}