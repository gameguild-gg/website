import React from 'react';

type Props = {
  children: React.ReactNode;
};

export function NotificationActions({ children }: Readonly<Props>) {
  return (<div>{children}</div>);
}
