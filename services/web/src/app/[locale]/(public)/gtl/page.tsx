import React from 'react';

export default function Component() {
    return (
        <div className="w-full min-h-screen bg-white">
            <div className="w-full overflow-hidden">
                <img
                    src="/assets/images/GTLBanner.png"
                    alt="Champlain Game Testing Banner"
                    className="w-full h-90 object-cover object-top"

                />
            </div>
            <div className="max-w-[1440px] mx-auto px-4 py-12">
                <h1 className="text-4xl font-bold mb-8 text-center">Game Testing Platform</h1>
                <div className="flex flex-col md:flex-row justify-center items-center space-y-4 md:space-y-0 md:space-x-8">
                    <a
                        href="/gtl/dev"
                        className="w-full md:w-96 h-48 bg-black text-white text-3xl font-semibold rounded-lg shadow-lg flex items-center justify-center transition-all duration-300 ease-in-out transform hover:bg-green-600 hover:-translate-y-1"
                    >
                        Dev
                    </a>
                    <a
                        href="/gtl/tester"
                        className="w-full md:w-96 h-48 bg-black text-white text-3xl font-semibold rounded-lg shadow-lg flex items-center justify-center transition-all duration-300 ease-in-out transform hover:bg-green-600 hover:-translate-y-1"
                    >
                        Tester
                    </a>
                </div>
            </div>
        </div>
    );
}
