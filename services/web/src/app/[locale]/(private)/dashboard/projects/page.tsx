import React from 'react';
import Link from 'next/link';
import {Button} from '@/components/ui/button';

export default function Page() {
  //   const games = await fetchGames();
  //
  //   return (
  //     <div className="container mx-auto py-8 px-4 md:px-6">
  //       <div className="grid grid-cols-1 gap-4 md:grid-cols-4 lg:grid-cols-4 mt-8">
  //         {games.map((game) => (
  //           <Link key={game.slug} href={`projects/${game.slug}`}>
  //             <GameCard game={game} />
  //           </Link>
  //         ))}
  //       </div>
  //     </div>
  //   );

  return (
    <div className="container bg-gray-100">
      <main className="max-w-7xl mx-auto px-4 sm:px-6 lg:px-8 py-8">
        <div className="bg-white shadow rounded-lg overflow-hidden">
          <div className="p-6">
            <h1 className="text-2xl font-semibold text-gray-900">
              Creator Dashboard
            </h1>
            <div className="mt-4 flex justify-end space-x-4">
              <div className="text-center">
                <div className="text-2xl font-bold">0</div>
                <div className="text-sm text-gray-500">Views</div>
              </div>
              <div className="text-center">
                <div className="text-2xl font-bold">0</div>
                <div className="text-sm text-gray-500">Downloads</div>
              </div>
              <div className="text-center">
                <div className="text-2xl font-bold">0</div>
                <div className="text-sm text-gray-500">Followers</div>
              </div>
            </div>
          </div>
          <div className="border-t border-gray-200">
            <nav className="flex">
              <Link
                href="#"
                className="px-6 py-3 text-sm font-medium text-pink-500 border-b-2 border-pink-500"
              >
                Projects
              </Link>
              <Link
                href="#"
                className="px-6 py-3 text-sm font-medium text-gray-500 hover:text-gray-700"
              >
                Analytics
              </Link>
              <Link
                href="#"
                className="px-6 py-3 text-sm font-medium text-gray-500 hover:text-gray-700"
              >
                Earnings
              </Link>
              <Link
                href="#"
                className="px-6 py-3 text-sm font-medium text-gray-500 hover:text-gray-700"
              >
                Promotions
              </Link>
              <Link
                href="#"
                className="px-6 py-3 text-sm font-medium text-gray-500 hover:text-gray-700"
              >
                Posts
              </Link>
              <Link
                href="#"
                className="px-6 py-3 text-sm font-medium text-gray-500 hover:text-gray-700"
              >
                Game jams
              </Link>
              <Link
                href="#"
                className="px-6 py-3 text-sm font-medium text-gray-500 hover:text-gray-700"
              >
                More
              </Link>
            </nav>
          </div>
          <div className="p-6">
            {/*<div className="bg-pink-50 border border-pink-100 rounded-md p-4 mb-6">*/}
            {/*  <p className="text-pink-800">*/}
            {/*    <span className="font-semibold">itch.io tips:</span> Tell us how*/}
            {/*    you build your projects • Try the Engines, Tools, & Devices*/}
            {/*    classification{' '}*/}
            {/*    <Link href="#" className="text-pink-600 hover:underline">*/}
            {/*      learn more →*/}
            {/*    </Link>*/}
            {/*  </p>*/}
            {/*</div>*/}
            <div className="text-center py-12">
              <h2 className="text-2xl font-semibold text-gray-700 mb-6">
                Are you a developer? Upload your first game
              </h2>
              <Button className="bg-pink-500 text-white hover:bg-pink-600" asChild>
                <Link href="/dashboard/projects/create">Create new project</Link>
              </Button>
              <div className="mt-4">
                <Link
                  href="#"
                  className="text-sm text-gray-500 hover:underline"
                >
                  Nah, take me to the games feed
                </Link>
              </div>
            </div>
          </div>
        </div>
      </main>
    </div>
  );
}
