import React from 'react';

type Props = {
  children: React.ReactNode;
};

export function NotificationContent({ children }: Readonly<Props>) {
  return (<div><p>{children}</p></div>);
}
