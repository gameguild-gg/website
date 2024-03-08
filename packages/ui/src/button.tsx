'use client';

import React from 'react';

interface Props extends React.ButtonHTMLAttributes<HTMLButtonElement> {
  children: React.ReactNode;
}

export function Button({ children, ...props}: Readonly<Props>) {
  return (<button {...props}>{children}</button>);
}
