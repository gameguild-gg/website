import React from "react";

type Props = React.HTMLAttributes<HTMLDivElement> & {
  children?: React.ReactNode;
};

function PageHeaderRoot({ children }: Readonly<Props>) {
  return (
    <div className="w-full">
      <div>
        {children}
      </div>
    </div>
  );
}

export { PageHeaderRoot };
