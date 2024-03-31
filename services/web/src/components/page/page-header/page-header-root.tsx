import React from "react";

type Props = React.HTMLAttributes<HTMLDivElement> & {
  children?: React.ReactNode;
};

function PageHeaderRoot({ children }: Readonly<Props>) {
  return (
    <header className="w-full">
        {children}
    </header>
  );
}

export { PageHeaderRoot };
