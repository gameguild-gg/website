import React from "react";

type Props = {
  children?: React.ReactNode;
};

export default function DashboardFooter({ children }: Readonly<Props>) {
  return (
    <footer className="flex w-full">
      <div className="flex h-20 items-center justify-between">
      </div>
    </footer>
  );
}