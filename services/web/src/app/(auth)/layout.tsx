import React from "react";

type AuthLayoutProps = {
    children: React.ReactNode;
};

async function AuthLayout({children}: AuthLayoutProps) {
    return <div>{children}</div>;
}

export default AuthLayout;
