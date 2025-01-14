import React from 'react';

type Props = React.HTMLAttributes<HTMLDivElement> & {
  children?: React.ReactNode;
};

function PageContent({ children }: Readonly<Props>) {
  return <div className="w-full p-12 m-2">{children}</div>;
}

export { PageContent };
