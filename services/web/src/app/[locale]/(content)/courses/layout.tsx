import React, {PropsWithChildren} from 'react';

export default async function Layout({children}: Readonly<PropsWithChildren>) {
  return (
    <div className="flex flex-1 min-h-full">
      {children}
    </div>
  );
}
