import React from 'react';

function CatchTheCat() {
  return (
    <div className="bg-gray-100 min-h-screen flex items-center justify-center">
      <form className="max-w-sm rounded overflow-hidden shadow-lg px-8 pt-6 pb-8 mb-4 border border-gray-200 bg-white">
        <div className="font-bold text-xl p-0">Catch The Cat</div>
        <p className="text-gray-700 text-base mb-1">
          Enter your password, upload your zip file, and press Submit:
        </p>
        <input
          className="shadow appearance-none border rounded w-full py-2 px-3 text-gray-700 leading-tight focus:outline-none focus:shadow-outline"
          id="password"
          type="text"
          placeholder="Password"
        />
        <input
          className="relative mb-2 mt-2 block w-full min-w-0 flex-auto rounded border border-solid border-neutral-300 bg-clip-padding px-3 py-[0.32rem] text-base font-normal text-neutral-700 transition duration-300 ease-in-out file:-mx-3 file:-my-[0.32rem] file:overflow-hidden file:rounded-none file:border-0 file:border-solid file:border-inherit file:bg-neutral-100 file:px-3 file:py-[0.32rem] file:text-neutral-700 file:transition file:duration-150 file:ease-in-out file:[border-inline-end-width:1px] file:[margin-inline-end:0.75rem] hover:file:bg-neutral-200 focus:border-primary focus:text-black focus:shadow-te-primary focus:outline-none focus:text-black"
          type="file"
          id="formFile"
        />
        <button className="w-full bg-blue-500 hover:bg-blue-700 text-white font-bold py-2 px-4 rounded focus:outline-none focus:shadow-outline" type="button">
          Submit
        </button>
      </form>
    </div>
  );
}

export default CatchTheCat;
