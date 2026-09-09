import { Providers } from '@/components/providers';
import '@/styles/globals.css';
import type { Metadata } from 'next';
import React from 'react';

export const metadata: Metadata = {
    applicationName: 'GameGuild Learning',
    title: {
        default: 'GameGuild Learning',
        template: '%s | GameGuild Learning',
    },
    description: 'GameGuild course and classroom experience.',
    manifest: '/manifest.webmanifest',
    icons: {
        icon: [{ url: '/favicon.svg', type: 'image/svg+xml' }],
        shortcut: [{ url: '/favicon.svg', type: 'image/svg+xml' }],
    },
};

export default function RootLayout({ children }: { children: React.ReactNode }) {
    return (
        <html lang="en" className="dark" suppressHydrationWarning>
            <body className="min-h-screen bg-background text-foreground antialiased">
                <Providers>{children}</Providers>
            </body>
        </html>
    );
}
