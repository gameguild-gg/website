import React from "react";
import { redirect } from "next/navigation";
import { auth } from "@/auth";

type DashboardLayoutProps = {
  children: React.ReactNode;
};

async function DashboardLayout({ children }: Readonly<DashboardLayoutProps>) {
  const session = await auth();

  if (!session?.user) {
    redirect("/sign-in");
  }

  return (<div>{children}</div>);
}

export default DashboardLayout;
