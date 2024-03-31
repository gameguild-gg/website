import React from "react";
import { Avatar, AvatarFallback, AvatarImage } from "@/components/ui/avatar";
import { Button } from "../ui/button";
import { Bell } from "lucide-react";

type Props = {
  children?: React.ReactNode;
};

export default function DashboardHeader({ children }: Readonly<Props>) {
  return (
    <header className="flex w-full">
      <div className="flex flex-grow h-20 items-center justify-between">
        <div className="hidden md:block">
          {/*    /!*TODO Convert user profile component here*!/*/}
          a
        </div>
        <div className="hidden md:block">
          {/*    /!*TODO Convert user profile component here*!/*/}
          {/*  /!* <!-- User Profile --> *!/*/}
          <div className="ml-4 flex items-center md:ml-6 gap-2">
            <Button variant="ghost" size="icon">
              <Bell className="size-6" />
            </Button>
            <Button variant="ghost" size="icon">
              <Avatar>
                <AvatarImage src="https://github.com/shadcn.png" alt="@shadcn" />
                <AvatarFallback>U</AvatarFallback>
              </Avatar>
            </Button>
          </div>
        </div>
        {/*  /!* <!-- Mobile Menu Toggle --> *!/*/}
        {/*  <div className="-mr-2 flex md:hidden">*/}
        {/*    /!*TODO Convert mobile menu toggle here*!/*/}
        {/*  </div>*/}
        {/*</div>*/}
      </div>
    </header>
  );
}