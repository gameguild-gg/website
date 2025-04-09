import React from 'react';

interface LayoutProps {
  header: React.ReactNode;
  left: React.ReactNode;
  right: React.ReactNode;
}

export default function Layout(props: LayoutProps) {
  const { header, left, right } = props;

  return (
    <div className="h-full flex flex-col overflow-hidden">
      <header className="flex-shrink-0 shadow-md z-10">{header}</header>
      <div className="flex-grow flex flex-col h-full overflow-hidden">
        <div className="flex-1 overflow-auto min-h-[50%] border-b border-gray-700">
          {left}
        </div>
        <div className="flex-1 overflow-auto min-h-[30%]">
          {right}
        </div>
      </div>
    </div>
  );
}
