"use client";


import React, { useState } from 'react'; // Correct import statement // Make sure useState is imported
import Link from 'next/link';
import { Button } from '@/components/ui/button';

export default function Page() {
    // State to track the number of games; replace with your dynamic data source as needed
    const  [numberOfGames, setNumberOfGames] = useState(0);
    const  [NumberOfViews, setNumberOfViews] = useState(1);
    const  [NumberOfDownloads, setNumberOfDownloads] = useState(2);
    const  [NumberOfFollowers, setNumberOfFollowers] = useState(3);
    const   [NumberOfTickets, setNumberOfTickets] = useState(999);


    // Handler to add a game (for demonstration purposes)
    const handleAddGame = () => {
        setNumberOfGames((prev) => prev + 1);
    };
    const AddView = () =>{
        setNumberOfViews((prev) => prev + 1);
    }
    const AddDownlaod = () =>{
        setNumberOfDownloads((prev) => prev + 1);
    }
    const AddFollowers = () =>{
        setNumberOfFollowers((prev) => prev + 1);
    }
    const AddTicket = () =>{
        setNumberOfTickets((prev) => prev + 1);
    }

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
                                <div className="text-2xl font-bold">{NumberOfViews}</div>
                                <div className="text-sm text-gray-500">Views</div>
                            </div>
                            <div className="text-center">
                                <div className="text-2xl font-bold">{NumberOfDownloads}</div>
                                <div className="text-sm text-gray-500">Downloads</div>
                            </div>
                            <div className="text-center">
                                <div className="text-2xl font-bold">{NumberOfFollowers}</div>
                                <div className="text-sm text-gray-500">Followers</div>
                            </div>
                            <div className="text-center">
                                <div className="text-2xl font-bold">{NumberOfTickets}</div>
                                <div className="text-sm text-gray-500">Tickets</div>
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
                    {/* Conditional rendering based on the number of games */}
                    {numberOfGames === 0 ? (
                        <div className="p-6">
                            <div className="text-center py-12">
                                <h2 className="text-2xl font-semibold text-gray-700 mb-6">
                                    Are you a developer? Upload your first game
                                </h2>
                                <Button
                                    className="bg-pink-500 text-white hover:bg-pink-600"
                                    asChild
                                >
                                    <Link href="/dashboard/projects/create">
                                        Create new project
                                    </Link>
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
                    ) : (
                        <div className="p-6">
                            <div className="text-center py-12">
                                <h2 className="text-2xl font-semibold text-gray-700 mb-6">
                                    You have {numberOfGames} game
                                    {numberOfGames > 1 ? 's' : ''}!
                                </h2>
                                <Button
                                    className="bg-blue-500 text-white hover:bg-blue-600"
                                    asChild
                                >
                                    <Link href="/dashboard/projects">
                                        View your projects
                                    </Link>
                                </Button>
                            </div>
                        </div>
                    )}

                </div>
            </main>
        </div>
    );
}
