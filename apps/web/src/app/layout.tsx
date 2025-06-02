import { PropsWithChildren } from 'react';
import '@/styles/globals.css';

// Since we have a `not-found.tsx` page on the root, a layout file
// is required, even if it's just passing children through.
export default async function Layout({ children }: Readonly<PropsWithChildren>) {
  return children;
}
