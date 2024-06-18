'use client';

import Link from 'next/link';
import Image from 'next/image';

export default function NotFound() {
  return (
    <html lang="en">
      <body>
        <div className="flex flex-grow flex-col min-h-[100vh] items-center justify-center space-y-4 text-center">
          <Image
            src={'/assets/images/placeholder.svg'}
            alt={'Acme Inc'}
            height="150"
            width="300"
            className="mx-auto aspect-[2/1] overflow-hidden rounded-lg object-contain object-center"
          />
          <div className="space-y-2">
            <h1 className="text-3xl font-bold tracking-tighter sm:text-4xl md:text-5xl">
              Uh oh! You&apos;re lost.
            </h1>
            <p className="max-w-[600px] text-gray-500 md:text-xl/relaxed lg:text-base/relaxed xl:text-xl/relaxed dark:text-gray-400">
              It looks like you found a glitch in the matrix. Don&apos;t worry,
              we&apos;ll get you back to safety.
            </p>
          </div>
          <Link
            className="inline-flex h-10 items-center justify-center rounded-md border border-gray-200 bg-white px-8 text-sm font-medium shadow-sm gap-1 transition-colors hover:bg-gray-100 hover:text-gray-900 focus-visible:outline-none focus-visible:ring-1 focus-visible:ring-gray-950 disabled:pointer-events-none disabled:opacity-50 dark:border-gray-800 dark:border-gray-800 dark:bg-gray-950 dark:hover:bg-gray-800 dark:hover:text-gray-50 dark:focus-visible:ring-gray-300"
            href="/"
          >
            Back to Safety
          </Link>
        </div>
      </body>
    </html>
  );
}
