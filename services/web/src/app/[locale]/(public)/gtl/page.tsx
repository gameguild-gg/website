export default function Component() {
    return (
        <div className="w-full min-h-screen bg-black">
            <div className="max-w-[1440px] mx-auto px-4 py-12">
                <h1 className="text-4xl font-bold mb-8 text-center text-white">Champlain College Game Testing Lab</h1>
                {/* Added mt-16 to push the buttons lower */}
                <div className="flex flex-col md:flex-row justify-center items-center md:space-x-12 space-y-8 md:space-y-0 mt-16">
                    <a
                        href="/gtl/owner"
                        className="group flex flex-col items-center justify-center w-full md:w-1/2 transition-all duration-300 ease-in-out transform hover:-translate-y-2 hover:scale-105"
                    >
                        <img
                            src="/assets/images/desktop_computer_line_art.png"
                            alt="Dev Button"
                            className="w-[400px] h-auto transition-all duration-300 ease-in-out group-hover:brightness-110 group-hover:grayscale-0 group-hover:shadow-lg rounded"
                        />
                        <p className="mt-4 text-2xl font-semibold text-center text-white group-hover:text-green-600 transition-all duration-300">
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
                            className="w-[500px] h-auto transition-all duration-300 ease-in-out group-hover:brightness-110 group-hover:grayscale-0 group-hover:shadow-lg rounded"
                        />
                        <p className="mt-4 text-2xl font-semibold text-center text-white group-hover:text-green-600 transition-all duration-300">
                            Test a Game
                        </p>
                    </a>
                </div>
            </div>
        </div>
    );
}
