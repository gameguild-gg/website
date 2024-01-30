import { Metadata } from 'next';
import React from 'react';
import './globals.css';
import { GoogleOAuthProvider } from '@react-oauth/google';
import {NotificationArgsProps} from "antd";

export const metadata: Metadata = {
  title: {
    template: '%s | Game Guild',
    default: 'Game Guild',
  },
  description: 'A awesome game development community',
  icons:{
    icon: 'favicon-32x32.png',
    shortcut: 'favicon-32x32.png',
    apple: 'apple-touch-icon.png'
  }
};

type Props = {
  children: React.ReactNode;
};
export default function RootLayout({ children }: Props) {
  return (
    <html lang="en">
      <body>
        {children}
      </body>
    </html>
  );
}
