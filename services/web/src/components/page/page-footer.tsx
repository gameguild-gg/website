import React from "react";

type Props = React.HTMLAttributes<HTMLDivElement> & {
  children?: React.ReactNode;
};

function PageFooter({ children }: Readonly<Props>) {
  return (
    <div className="w-full">
      <div className="mx-auto max-w-7xl p-6">
        {children}
      </div>
    </div>
  );
}

export { PageFooter };
