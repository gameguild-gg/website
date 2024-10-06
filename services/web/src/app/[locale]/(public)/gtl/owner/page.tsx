'use client';

import React, { useState, useEffect } from 'react';
import { useRouter } from 'next/navigation';
import { ScrollArea } from '@/components/ui/scroll-area';
import AnalyticsGraphs from './AnalyticsGraphs';
import { TicketApi, ProjectApi } from '@game-guild/apiclient/api';
import { getSession } from 'next-auth/react';
import { Api } from '@game-guild/apiclient/models';

const GameCard = ({ title, description }) => (
  <div className="bg-gray-800 rounded-lg overflow-hidden shadow-lg">
    <div className="h-48 bg-gray-700 flex items-center justify-center">
      <span className="text-gray-500">Image Placeholder</span>
    </div>
    <div className="p-4">
      <div className="flex justify-between items-center mb-2">
        <h3 className="text-lg font-semibold text-white">{title}</h3>
        <span className="bg-gray-700 text-xs font-semibold text-gray-300 px-2 py-1 rounded">
          draft
        </span>
      </div>
      <p className="text-sm text-gray-400">{description}</p>
    </div>
  </div>
);

// GameGrid component
const GameGrid = ({ games }) => (
  <ScrollArea className="h-[calc(100vh-300px)]">
    <div className="grid grid-cols-1 md:grid-cols-2 lg:grid-cols-3 gap-6 p-6">
      {games.map((game, index) => (
        <GameCard
          key={index}
          title={game.title}
          description={game.description}
        />
      ))}
    </div>
  </ScrollArea>
);
// TicketBox component
const TicketBox = ({ ticket, onClick }) => (
  <div
    onClick={() => onClick(ticket)}
    className="bg-gray-800 rounded-lg overflow-hidden shadow-lg p-4 hover:bg-gray-700 transition-colors duration-200 cursor-pointer"
  >
    <div className="flex justify-between items-center mb-2">
      <h3 className="text-lg font-semibold text-white">{ticket.title}</h3>
      <span className="text-xs font-semibold text-gray-300 px-2 py-1 rounded bg-gray-700">
        {ticket.status}
      </span>
    </div>
    <p className="text-sm text-gray-400">
      Submitted by: {ticket.owner_username}
    </p>
  </div>
);

// TicketGrid component
const TicketGrid = ({ tickets, onTicketClick }) => (
  <ScrollArea className="h-[calc(100vh-300px)]">
    <div className="grid grid-cols-1 sm:grid-cols-2 md:grid-cols-3 lg:grid-cols-6 gap-4 p-6">
      {tickets.map((ticket, index) => (
        <TicketBox
          key={index}
          ticket={ticket}
          onClick={onTicketClick} // Correct usage of onClick
        />
      ))}
    </div>
  </ScrollArea>
);

// VideoBox component
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

// VideoGrid component
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

const apiTicket = new TicketApi({
  basePath: process.env.NEXT_PUBLIC_API_URL,
});

const apiProject = new ProjectApi({
  basePath: process.env.NEXT_PUBLIC_API_URL,
});

export enum VisibilityEnum {
  DRAFT = 'DRAFT', // not visible to the public
  PUBLISHED = 'PUBLISHED', // published and visible
  FUTURE = 'FUTURE', // scheduled for future publication
  PENDING = 'PENDING', // pending approval
  PRIVATE = 'PRIVATE', // only visible to the author
  TRASH = 'TRASH', // marked for deletion
}

// eslint-disable-next-line @typescript-eslint/explicit-function-return-type
export default function Page() {
  const initialVideos = [
    {
      videoStatus: 'Watched',
      videoTitle: 'Gameplay 1',
      submitter: 'User A',
      linkedToTicket: true,
    },
    {
      videoStatus: 'Unwatched',
      videoTitle: 'Gameplay 2',
      submitter: 'User B',
      linkedToTicket: false,
    },
    {
      videoStatus: 'Watched',
      videoTitle: 'Gameplay 3',
      submitter: 'User C',
      linkedToTicket: true,
    },
    {
      videoStatus: 'Unwatched',
      videoTitle: 'Gameplay 4',
      submitter: 'User D',
      linkedToTicket: false,
    },
    {
      videoStatus: 'Unwatched',
      videoTitle: 'Gameplay 5',
      submitter: 'User E',
      linkedToTicket: false,
    },
    {
      videoStatus: 'Watched',
      videoTitle: 'Gameplay 6',
      submitter: 'User F',
      linkedToTicket: true,
    },
  ];

  const [activeTab, setActiveTab] = useState('Projects');
  const [ticketFilter, setTicketFilter] = useState('All');
  const [tickets, setTickets] = useState<Api.TicketEntity[]>([]);
  const [projects, setProjects] = useState<Api.ProjectEntity[]>([]);
  const [videos] = useState(initialVideos);

  const router = useRouter();

  // Define the handleTicketClick function once
  const handleTicketClick = (ticket) => {
    localStorage.setItem('selectedTicket', JSON.stringify(ticket));
    router.push(`/gtl/owner/Tickets/`);
  };

  const fetchData = async () => {
    const session = await getSession();
    console.log(session);
    const gotProjects =
      await apiProject.getManyBaseProjectControllerProjectEntity(
        {},
        {
          headers: {
            Authorization: `Bearer ${session?.accessToken}`,
          },
        },
      );

    const gotTickets = await apiTicket.getManyBaseTicketControllerTicketEntity(
      {},
      {
        headers: {
          Authorization: `Bearer ${session?.accessToken}`,
        },
      },
    );

    if (gotTickets && Array.isArray(gotTickets.body)) {
      setTickets(gotTickets.body);
    }

    if (gotProjects && Array.isArray(gotProjects.body)) {
      setProjects(gotProjects.body);
    } else {
      console.error('Unexpected response format:', gotProjects);
    }
  };

  const [videoFilter, setVideoFilter] = useState('All');

  const filteredTickets = tickets.filter(
    (ticket) => ticketFilter === 'All' || ticket.status === ticketFilter,
  );
  // Filter videos based on the selected filter status
  const filteredVideos = videos.filter(
    (video) => videoFilter === 'All' || video.videoStatus === videoFilter,
  );

  const renderContent = () => {
    switch (activeTab) {
      case 'Projects':
        return <GameGrid games={projects} />;
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
                <option value="All">ALL</option>
                <option value="OPEN">OPEN</option>
                <option value="IN_PROGRESS">IN PROGRESS</option>
                <option value="RESOLVED">RESOLVED</option>
                <option value="CLOSED">CLOSED</option>
              </select>
            </div>
            <TicketGrid
              tickets={filteredTickets}
              onTicketClick={handleTicketClick}
            />
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
        return <GameGrid games={projects} />;
    }
  };

  useEffect(() => {
    fetchData();
  }, []);

  return (
    <div className="bg-black text-white min-h-screen flex justify-center items-start pt-4 overflow-hidden">
      <main className="bg-gray-900 w-full max-w-7xl mx-auto px-4 sm:px-6 lg:px-8 py-6 rounded-lg shadow-lg">
        <div className="flex-1">
          <div className="p-6">
            <h1 className="text-2xl font-semibold text-white">
              Creator Dashboard
            </h1>
          </div>
          <div className="border-t border-gray-700">
            <nav className="flex">
              {['Projects', 'Analytics', 'Submitted Tickets', 'Videos'].map(
                (tab) => (
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
                ),
              )}
            </nav>
          </div>
          <div className="p-6">{renderContent()}</div>
        </div>
      </main>
    </div>
  );
}
