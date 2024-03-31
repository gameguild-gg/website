import React from "react";
import { redirect } from "next/navigation";
import { auth } from "@/auth";

type Props = {
  children: React.ReactNode;
};

export default async function Layout({ children }: Readonly<Props>) {
  const session = await auth();

  if (!session?.user) {
    redirect("/sign-in");
  }

  return ({ children });
}
