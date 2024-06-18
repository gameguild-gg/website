import React from 'react';

type Props = React.HTMLAttributes<HTMLDivElement> & {
  children?: React.ReactNode;
};

function PageRoot({ children }: Readonly<Props>) {
  return <div className="h-full">{children}</div>;
}

export { PageRoot };
