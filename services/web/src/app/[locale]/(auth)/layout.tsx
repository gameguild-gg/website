import React from "react";

type AuthLayoutProps = {
  children: React.ReactNode;
};

async function AuthLayout({ children }: Readonly<AuthLayoutProps>) {
  return (
    <div>
      {children}
    </div>
  );
}

export default AuthLayout;
