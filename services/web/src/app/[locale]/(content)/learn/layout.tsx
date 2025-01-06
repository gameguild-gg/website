/*
import './globals.css'
import { Inter } from 'next/font/google'

const inter = Inter({ subsets: ['latin'] })

export const metadata = {
  title: 'GameGuild Learning Platform',
  description: 'An interactive platform for learning game development',
}

export default function RootLayout({
  children,
}: {
  children: React.ReactNode
}) {
  return (
    <html lang="en">
      <body className={inter.className}>{children}</body>
    </html>
  )
}
  */

import React, { PropsWithChildren } from 'react';
import Header from '@/components/common/header';

export default async function Layout({
  children,
}: Readonly<PropsWithChildren>) {
  return (
    <div className="flex flex-auto flex-col">
      <Header />
      <div className="flex flex-auto h-full w-full">{children}</div>
    </div>
  );
}


