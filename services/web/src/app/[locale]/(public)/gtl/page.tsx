import React from 'react';

export default function Component() {
    return (
        <div className="w-full min-h-screen bg-gradient-to-b from-white via-gray-50 to-gray-100">
            <div className="w-full overflow-hidden">
                <img
                    src="/assets/images/GTLBanner.png"
                    alt="Champlain Game Testing Banner"
                    className="w-full h-90 object-cover object-[top -10%]"
                />
            </div>
            <div className="max-w-[1440px] mx-auto px-4 py-12">
                <h1 className="text-4xl font-bold mb-8 text-center text-gray-800">Make Your Choice</h1>
                <div className="flex flex-col md:flex-row justify-center items-center md:space-x-12 space-y-8 md:space-y-0">
                    <a
                        href="/gtl/dev"
                        className="group flex flex-col items-center justify-center w-full md:w-1/2 transition-all duration-300 ease-in-out transform hover:-translate-y-2 hover:scale-105"
                    >
                        <img
                            src="/assets/images/desktop_computer_line_art.png"
                            alt="Dev Button"
                            className="w-[400px] h-auto transition-all duration-300 ease-in-out group-hover:brightness-110 group-hover:grayscale-0 group-hover:shadow-lg rounded"
                        />
                        <p className="mt-4 text-2xl font-semibold text-center text-gray-700 group-hover:text-green-600 transition-all duration-300">
                            Build a Game
                        </p>
                    </a>
                    <a
                        href="/gtl/tester"
                        className="group flex flex-col items-center justify-center w-full md:w-1/2 transition-all duration-300 ease-in-out transform hover:-translate-y-2 hover:scale-105"
                    >
                        <img
                            src="/assets/images/XboxController.png"
                            alt="Tester Button"
                            className="w-[400px] h-auto transition-all duration-300 ease-in-out group-hover:brightness-110 group-hover:grayscale-0 group-hover:shadow-lg rounded"
                        />
                        <p className="mt-4 text-2xl font-semibold text-center text-gray-700 group-hover:text-green-600 transition-all duration-300">
                            Test a Game
                        </p>
                    </a>
                </div>
            </div>
        </div>
    );
}
