import React from "react";
import Link from "next/link";
import { UserProfile } from "@/components/page/user-profile";

type HeaderMenuLink = {
  label: string;
  href: string;
};

type Props = React.HTMLAttributes<HTMLDivElement> & {
  links: HeaderMenuLink[];
};

function PageHeaderMenu({ links }: Readonly<Props>) {
  return (
    <nav className="">
      {/* <!-- Desktop Menu --> */}
      <div className="mx-auto max-w-7xl px-4 sm:px-6 lg:px-8">
        <div className="flex h-20 items-center justify-between">
          {/* <!-- Header Navigation --> */}
          <div className="flex items-center">
            <div className="flex-shrink-0">
              {/*TODO Add logo component here*/}
            </div>
            <div className="hidden md:block">
              <div className="ml-10 flex items-baseline space-x-4">
                {links.map((link, index) =>
                  <Link className="px-3 py-2 text-sm font-medium" href={link.href}
                        key={index}>{link.label}</Link>
                )}
                {/* TODO fix the navigation link, it may should be a component?*/}
              </div>
            </div>
          </div>
          {/* <!-- User Profile --> */}
          <div className="hidden md:block">
            {/*TODO Add user profile component here*/}
            {/*<UserProfile />*/}
          </div>
          {/* <!-- Mobile Menu Toggle --> */}
          <div className="-mr-2 flex md:hidden">
            {/*TODO Add mobile menu toggle here*/}
          </div>
        </div>
      </div>
      {/* <!-- Mobile Menu --> */}
      <div className="md:hidden" id="mobile-menu">
        {/* <!-- Navigation --> */}
        <div className="space-y-1 px-2 pb-3 pt-2 sm:px-3">
          {/* TODO add navigation links here */}
          {links.map((link, index) =>
            <Link className="text-gray-300 hover:text-white block rounded-md px-3 py-2 text-base font-medium"
                  href={link.href} key={index}>{link.label}</Link>
          )}
          {/* TODO fix the navigation link, it may should be a component?*/}
        </div>
        {/* <!-- User Profile --> */}
        <div className="border-t border-gray-700 pb-3 pt-4">
          <div className="flex items-center px-5">
            {/* TODO add user profile here */}
          </div>
          <div className="mt-3 space-y-1 px-2">
            {/* TODO add user profile menu here */}
          </div>
        </div>
      </div>
    </nav>
  );
}

export { PageHeaderMenu };
