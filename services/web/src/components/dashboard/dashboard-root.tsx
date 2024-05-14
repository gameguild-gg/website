import React from "react";

type Props = {
  children?: React.ReactNode;
};

export default function DashboardRoot({ children }: Readonly<Props>) {
  return (
    <div className="flex flex-grow justify-between bg-gray-100">
      {children}
    </div>
  );
}