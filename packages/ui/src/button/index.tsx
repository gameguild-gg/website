"use client";

import React from "react";

type ButtonProps = React.ButtonHTMLAttributes<HTMLButtonElement> & {
  children: React.ReactNode;
}

function Button({ children, ...props }: Readonly<ButtonProps>) {
  return (
    <button {...props}>
      {children}
    </button>
  );
}

export default Button;
