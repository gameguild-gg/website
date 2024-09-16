"use client";

import React, { useState } from 'react';
import Link from 'next/link';
import { Button } from '@/components/ui/button';
import { ScrollArea } from "@/components/ui/scroll-area";
import AnalyticsGraphs from './AnalyticsGraphs'

const GameCard = ({ title, description }) => (
    <div className="bg-gray-800 rounded-lg overflow-hidden shadow-lg">
        <div className="h-48 bg-gray-700 flex items-center justify-center">
            <span className="text-gray-500">Image Placeholder</span>
        </div>
        <div className="p-4">
            <div className="flex justify-between items-center mb-2">
                <h3 className="text-lg font-semibold text-white">{title}</h3>
                <span className="bg-gray-700 text-xs font-semibold text-gray-300 px-2 py-1 rounded">draft</span>
            </div>
            <p className="text-sm text-gray-400">{description}</p>
        </div>
    </div>
);

const GameGrid = ({ games }) => (
    <ScrollArea className="h-[calc(100vh-300px)]">
        <div className="grid grid-cols-1 md:grid-cols-2 lg:grid-cols-3 gap-6 p-6">
            {games.map((game, index) => (
                <GameCard key={index} title={game.title} description={game.description} />
            ))}
        </div>
    </ScrollArea>
);

export default function Page() {
    const [activeTab, setActiveTab] = useState('Projects');
    const [numberOfGames, setNumberOfGames] = useState(5); // Changed to 5 for demonstration
    const [NumberOfViews, setNumberOfViews] = useState(1);
    const [NumberOfDownloads, setNumberOfDownloads] = useState(2);
    const [NumberOfFollowers, setNumberOfFollowers] = useState(3);
    const [NumberOfTickets, setNumberOfTickets] = useState(999);

    // Sample game data
    const games = [
        { title: "Game 1", description: "Description 1" },
        { title: "Game 2", description: "Description 2" },
        { title: "Game 3", description: "Description 3" },
        { title: "Game 4", description: "Description 4" },
        { title: "Game 5", description: "Description 5" },
    ];

    const renderContent = () => {
        switch (activeTab) {
            case 'Projects':
                return <GameGrid games={games} />;
            case 'Analytics':
                return <AnalyticsGraphs />; // Displays the AnalyticsGraphs component when "Analytics" is selected
            case 'Earnings':
                return <div>Earnings Content</div>;
            case 'Submitted Tickets':
                return <div>Submitted Tickets</div>;
            case 'Videos':
                return <div>Posts Content</div>;
            default:
                return <GameGrid games={games} />;
        }
    };

    return (
        <div className="bg-black text-white min-h-screen flex justify-center items-start pt-4 overflow-hidden">
            <main className="bg-gray-900 w-full max-w-7xl mx-auto px-4 sm:px-6 lg:px-8 py-6 rounded-lg shadow-lg">
                <div className="flex-1">
                    <div className="p-6">
                        <h1 className="text-2xl font-semibold text-white">
                            Creator Dashboard
                        </h1>
                        <div className="mt-4 flex justify-end space-x-4">
                            <div className="text-center">
                                <div className="text-2xl font-bold text-white">{NumberOfViews}</div>
                                <div className="text-sm text-gray-400">Views</div>
                            </div>
                            <div className="text-center">
                                <div className="text-2xl font-bold text-white">{NumberOfDownloads}</div>
                                <div className="text-sm text-gray-400">Downloads</div>
                            </div>
                            <div className="text-center">
                                <div className="text-2xl font-bold text-white">{NumberOfFollowers}</div>
                                <div className="text-sm text-gray-400">Followers</div>
                            </div>
                            <div className="text-center">
                                <div className="text-2xl font-bold text-white">{NumberOfTickets}</div>
                                <div className="text-sm text-gray-400">Tickets</div>
                            </div>
                        </div>
                    </div>
                    <div className="border-t border-gray-700">
                        <nav className="flex">
                            {['Projects', 'Analytics', 'Earnings', 'Submitted Tickets', 'Videos'].map((tab) => (
                                <button
                                    key={tab}
                                    onClick={() => setActiveTab(tab)}
                                    className={`px-6 py-3 text-sm font-medium ${
                                        activeTab === tab
                                            ? 'text-pink-400 border-b-2 border-pink-400'
                                            : 'text-gray-400 hover:text-gray-200'
                                    }`}
                                >
                                    {tab}
                                </button>
                            ))}
                        </nav>
                    </div>
                    {numberOfGames === 0 ? (
                        <div className="p-6">
                            <div className="text-center py-12">
                                <h2 className="text-2xl font-semibold text-gray-700 mb-6">
                                    Are you a developer? Upload your first game
                                </h2>
                                <Button className="bg-pink-500 text-white hover:bg-pink-600" asChild>
                                    <Link href="/dashboard/projects/create">
                                        Create new project
                                    </Link>
                                </Button>
                                <div className="mt-4">
                                    <Link href="#" className="text-sm text-gray-500 hover:underline">
                                        Nah, take me to the games feed
                                    </Link>
                                </div>
                            </div>
                        </div>
                    ) : (
                        <div className="p-6">{renderContent()}</div>
                    )}
                </div>
            </main>
        </div>
    );
}
