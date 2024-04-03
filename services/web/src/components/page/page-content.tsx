import React from "react";

type Props = React.HTMLAttributes<HTMLDivElement> & {
  children?: React.ReactNode;
};

function PageContent({ children }: Readonly<Props>) {
  return (
    <div className="w-screen p-0 m-0">
      {children}
    </div>
  );
}

export { PageContent };
