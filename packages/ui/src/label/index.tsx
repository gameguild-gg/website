"use client";

import React from "react";

type ButtonProps = React.LabelHTMLAttributes<HTMLLabelElement> & {
  children: React.ReactNode;
}

function Label({ children, ...props }: Readonly<ButtonProps>) {
  return (
    <label
      className={"text-sm font-medium leading-none peer-disabled:cursor-not-allowed peer-disabled:opacity-70"}
      {...props}
    >
      {children}
    </label>
  );
}

export default Label;
