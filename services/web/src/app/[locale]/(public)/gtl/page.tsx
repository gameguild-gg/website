import React from 'react';

export default async function Page() {
  return (
    // todo: show a feed of game versions sort by the last updated date.
    // show tags if it was already tested or not
    <div className="w-full h-full bg-neutral-100">
      <div className="w-[1440px] max-w-full min-h-screen mx-auto bg-white p-0 lg:flex justify-around">
        <div className="w-full m-0 p-0 bg-[url('/assets/images/placeholder.svg')] flex justify-around">
          <a
            href="/gtl/tester"
            className="rounded-lg bg-blue-500 text-white text-2xl font-semibold p-1 m-auto h-10 shadow"
          >
            I want to test games
          </a>
        </div>
        <div className="w-full m-0 p-0 bg-[url('/assets/images/placeholder.svg')] flex justify-around">
          <a
            href="/gtl/owner"
            className="rounded-lg bg-blue-500 text-white text-2xl font-semibold p-1 m-auto h-10 shadow"
          >
            I want my games tested
          </a>
        </div>
      </div>
    </div>
  );
}
