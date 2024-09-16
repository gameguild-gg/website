"use client";

import React, { useState } from 'react';
import Link from 'next/link';
import { Button } from '@/components/ui/button';
import { ScrollArea } from "@/components/ui/scroll-area";
import AnalyticsGraphs from './AnalyticsGraphs';

// GameCard component
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

// GameGrid component wrapping after 3 items
const GameGrid = ({ games }) => (
    <ScrollArea className="h-[calc(100vh-300px)]">
        <div className="grid grid-cols-1 md:grid-cols-2 lg:grid-cols-3 gap-6 p-6">
            {games.map((game, index) => (
                <GameCard key={index} title={game.title} description={game.description} />
            ))}
        </div>
    </ScrollArea>
);

// TicketBox component
const TicketBox = ({ ticketStatus, gameName, submitter }) => (
    <div className="bg-gray-800 rounded-lg overflow-hidden shadow-lg p-4">
        <div className="flex justify-between items-center mb-2">
            <h3 className="text-lg font-semibold text-white">{gameName}</h3>
            <span className="text-xs font-semibold text-gray-300 px-2 py-1 rounded bg-gray-700">
                {ticketStatus}
            </span>
        </div>
        <p className="text-sm text-gray-400">Submitted by: {submitter}</p>
    </div>
);

// TicketGrid component wrapping after 6 items
const TicketGrid = ({ tickets }) => (
    <ScrollArea className="h-[calc(100vh-300px)]">
        <div className="grid grid-cols-1 sm:grid-cols-2 md:grid-cols-3 lg:grid-cols-6 gap-4 p-6">
            {tickets.map((ticket, index) => (
                <TicketBox key={index} ticketStatus={ticket.ticketStatus} gameName={ticket.gameName} submitter={ticket.submitter} />
            ))}
        </div>
    </ScrollArea>
);

// VideoBox component for displaying individual videos
const VideoBox = ({ videoStatus, videoTitle, submitter, linkedToTicket }) => (
    <div className="bg-gray-800 rounded-lg overflow-hidden shadow-lg p-4">
        <div className="flex justify-between items-center mb-2">
            <h3 className="text-lg font-semibold text-white">{videoTitle}</h3>
            <span className="text-xs font-semibold text-gray-300 px-2 py-1 rounded bg-gray-700">
                {videoStatus}
            </span>
        </div>
        <p className="text-sm text-gray-400">Submitted by: {submitter}</p>
        {linkedToTicket && (
            <span className="text-sm text-pink-400 mt-2 inline-block">
                Linked to Ticket
            </span>
        )}
    </div>
);

// VideoGrid component wrapping after 6 items
const VideoGrid = ({ videos }) => (
    <ScrollArea className="h-[calc(100vh-300px)]">
        <div className="grid grid-cols-1 sm:grid-cols-2 md:grid-cols-3 lg:grid-cols-6 gap-4 p-6">
            {videos.map((video, index) => (
                <VideoBox
                    key={index}
                    videoStatus={video.videoStatus}
                    videoTitle={video.videoTitle}
                    submitter={video.submitter}
                    linkedToTicket={video.linkedToTicket}
                />
            ))}
        </div>
    </ScrollArea>
);

export default function Page() {
    const games = [
        { title: "Game 1", description: "Description 1" },
        { title: "Game 2", description: "Description 2" },
        { title: "Game 3", description: "Description 3" },
        { title: "Game 4", description: "Description 4" },
        { title: "Game 5", description: "Description 5" },
    ];

    const inatialTickets = [
        { ticketStatus: "Open", gameName: "Game 1", submitter: "User A" },
        { ticketStatus: "Open", gameName: "Game 2", submitter: "User B" },
        { ticketStatus: "Open", gameName: "Game 3", submitter: "User C" },
        { ticketStatus: "Open", gameName: "Game 4", submitter: "User D" },
        { ticketStatus: "Open", gameName: "Game 5", submitter: "User E" },
        { ticketStatus: "Closed", gameName: "Game 6", submitter: "User F" },
    ];

    const initialVideos = [
        { videoStatus: "Watched", videoTitle: "Gameplay 1", submitter: "User A", linkedToTicket: true },
        { videoStatus: "Unwatched", videoTitle: "Gameplay 2", submitter: "User B", linkedToTicket: false },
        { videoStatus: "Watched", videoTitle: "Gameplay 3", submitter: "User C", linkedToTicket: true },
        { videoStatus: "Unwatched", videoTitle: "Gameplay 4", submitter: "User D", linkedToTicket: false },
        { videoStatus: "Unwatched", videoTitle: "Gameplay 5", submitter: "User E", linkedToTicket: false },
        { videoStatus: "Watched", videoTitle: "Gameplay 6", submitter: "User F", linkedToTicket: true },
    ];

    const [activeTab, setActiveTab] = useState('Projects');
    const [numberOfGames, setNumberOfGames] = useState(games.length);
    const [NumberOfViews, setNumberOfViews] = useState(1);
    const [NumberOfDownloads, setNumberOfDownloads] = useState(2);
    const [NumberOfFollowers, setNumberOfFollowers] = useState(3);
    const [NumberOfTickets, setNumberOfTickets] = useState(inatialTickets.length);
    const [ticketFilter, setTicketFilter] = useState('All');
    const [tickets, setTickets] = useState(inatialTickets);
    const [videos, setVideos] = useState(initialVideos);
    const [videoFilter, setVideoFilter] = useState('All');

    // Filter tickets based on the selected filter status
    const filteredTickets = tickets.filter(ticket =>
        ticketFilter === 'All' || ticket.ticketStatus === ticketFilter
    );

    // Filter videos based on the selected filter status
    const filteredVideos = videos.filter(video =>
        videoFilter === 'All' || video.videoStatus === videoFilter
    );

    const renderContent = () => {
        switch (activeTab) {
            case 'Projects':
                return <GameGrid games={games} />;
            case 'Analytics':
                return <AnalyticsGraphs />;
            case 'Submitted Tickets':
                return (
                    <div>
                        <div className="flex justify-end mb-4">
                            <select
                                className="bg-gray-800 text-white p-2 rounded"
                                value={ticketFilter}
                                onChange={(e) => setTicketFilter(e.target.value)}
                            >
                                <option value="All">All</option>
                                <option value="Open">Open</option>
                                <option value="Closed">Closed</option>
                            </select>
                        </div>
                        <TicketGrid tickets={filteredTickets} />
                    </div>
                );
            case 'Videos':
                return (
                    <div>
                        <div className="flex justify-end mb-4">
                            <select
                                className="bg-gray-800 text-white p-2 rounded"
                                value={videoFilter}
                                onChange={(e) => setVideoFilter(e.target.value)}
                            >
                                <option value="All">All</option>
                                <option value="Watched">Watched</option>
                                <option value="Unwatched">Unwatched</option>
                            </select>
                        </div>
                        <VideoGrid videos={filteredVideos} />
                    </div>
                );
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
                            {['Projects', 'Analytics', 'Submitted Tickets', 'Videos'].map((tab) => (
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
