import React from "react";

type Props = {
  children: React.ReactNode;
};

export default async function Layout({ children }: Readonly<Props>) {
  return (
    <div className="absolute inset-0 w-full h-full z-50">
      <div className="h-full backdrop-blur-md ">
        <div className="h-full bg-black/85">
          <div className="flex h-full  ">
            <div
              className="flex w-96 bg-center bg-cover bg-[url('https://rarible.com/public/9069a74d3116247a914c.jpg')] p-8">

            </div>
            <div className="flex flex-grow p-8">
              {children}
            </div>
          </div>
        </div>
      </div>
    </div>
  );
}

