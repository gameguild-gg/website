import React from "react";

type Props = React.HTMLAttributes<HTMLDivElement> & {
  children?: React.ReactNode;
};

function PageContent({ children }: Readonly<Props>) {
  return (
    <div className="w-full">
      {children}
    </div>
  );
}

export { PageContent };
