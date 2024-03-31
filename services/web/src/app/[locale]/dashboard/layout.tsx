import React from "react";
import Dashboard from "@/components/dashboard";

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
    <Dashboard>
      <Dashboard.Sidebar />
      <Dashboard.Content size="wide">
        <Dashboard.Header />
        <Dashboard.Viewport>
          {children}
        </Dashboard.Viewport>
        <Dashboard.Footer />
      </Dashboard.Content>
    </Dashboard>
  );
}
