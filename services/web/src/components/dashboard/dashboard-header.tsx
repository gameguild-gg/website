import React from "react";

type Props = {
  children?: React.ReactNode;
};

export default function DashboardHeader({ children }: Readonly<Props>) {
  return (
    <header className="flex w-full">
      <div className="flex h-20 items-center justify-between">
        {/*<div className="flex items-center">*/}
        {/*  /!* <!-- User Profile --> *!/*/}
        {/*  <div className="hidden md:block">*/}
        {/*    /!*TODO Convert user profile component here*!/*/}
        {/*  </div>*/}
        {/*  /!* <!-- Mobile Menu Toggle --> *!/*/}
        {/*  <div className="-mr-2 flex md:hidden">*/}
        {/*    /!*TODO Convert mobile menu toggle here*!/*/}
        {/*  </div>*/}
        {/*</div>*/}
      </div>
    </header>
  );
}