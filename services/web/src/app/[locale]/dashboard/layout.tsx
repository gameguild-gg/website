import React from "react";
import { redirect } from "next/navigation";
import { auth } from "@/auth";

type Props = {
  children: React.ReactNode;
};

export default async function Layout({ children }: Readonly<Props>) {
  // const session = await auth();
  //
  // if (!session?.user) {
  //   redirect("/sign-in");
  // }

  return (
    <div className="flex flex-grow justify-between">
      <div className="flex h-full">
        <div className="flex w-56">
          {/* TODO Convert into a navbar component */}
          {/* TODO Add an option to switch between left and right side layout */}
          {/* TODO Add an option to switch between vertical, collapsed and horizontal side layout */}
        </div>
      </div>
      <div className="flex flex-grow flex-col">
        <header className="flex w-full">
          {/* TODO Convert into a header component */}
          <div className="">
            <div className="flex h-20 items-center justify-between">
              <div className="flex items-center">
                {/* <!-- User Profile --> */}
                <div className="hidden md:block">
                  {/*TODO Convert user profile component here*/}
                </div>
                {/* <!-- Mobile Menu Toggle --> */}
                <div className="-mr-2 flex md:hidden">
                  {/*TODO Convert mobile menu toggle here*/}
                </div>
              </div>
            </div>
          </div>
        </header>
        <main className="flex flex-grow">
          {/* TODO Convert into a content component */}
          <div className="container">
            {/* TODO Add an option to switch between compact and wide layout */}
            {children}
          </div>
        </main>
        <footer className="flex w-full">
          {/* TODO Convert into a footer component */}
          <div className="flex h-20 items-center justify-between">

          </div>
        </footer>
      </div>
    </div>
  );
}
