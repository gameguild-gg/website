'use client'; // Error boundaries must be Client Components

import React from 'react';
import Link from 'next/link';

type Props = {
  error: Error & { digest?: string };
  reset: () => void;
};

export default function GlobalError({ error, reset }: Readonly<Props>) {
  return (
    <html>
      <body className="flex flex-col items-center justify-center min-h-screen bg-gray-100 text-gray-900">
        <div className="text-center space-y-4">
          <h1 className="text-3xl font-bold">Something went wrong</h1>
          <p className="text-gray-500">
            An unexpected error has occurred. Please try again later.
          </p>
          <div className="space-x-4">
            <button
              onClick={() => reset()}
              className="inline-flex items-center justify-center rounded-md border border-gray-200 bg-white px-4 py-2 text-sm font-medium shadow-sm transition-colors hover:bg-gray-100 hover:text-gray-900 focus-visible:outline-none focus-visible:ring-1 focus-visible:ring-gray-950 disabled:pointer-events-none disabled:opacity-50 dark:border-gray-800 dark:bg-gray-950 dark:hover:bg-gray-800 dark:hover:text-gray-50 dark:focus-visible:ring-gray-300"
            >
              Try again
            </button>
            <Link
              href="/"
              className="inline-flex items-center justify-center rounded-md border border-gray-200 bg-white px-4 py-2 text-sm font-medium shadow-sm transition-colors hover:bg-gray-100 hover:text-gray-900 focus-visible:outline-none focus-visible:ring-1 focus-visible:ring-gray-950 disabled:pointer-events-none disabled:opacity-50 dark:border-gray-800 dark:bg-gray-950 dark:hover:bg-gray-800 dark:hover:text-gray-50 dark:focus-visible:ring-gray-300"
            >
              Go to Home
            </Link>
          </div>
        </div>
      </body>
    </html>
  );
}
