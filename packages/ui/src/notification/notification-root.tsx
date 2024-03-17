import React from 'react';

type Props = {
  children: React.ReactNode;
};

export function NotificationRoot({ children }: Readonly<Props>) {
  return (<div>{children}</div>);
}
