import React from "react";

type Props = {
  children?: React.ReactNode;
};

export default function DashboardViewport({ children }: Readonly<Props>) {
  return (
    <main className="flex flex-grow">
      {children}
    </main>
  );
}