'use client';
import React from 'react';

type Props = {
  children: React.ReactNode;
};
export default function AuthLayout({ children }: Props) {
  return <div>{children}</div>;
}
