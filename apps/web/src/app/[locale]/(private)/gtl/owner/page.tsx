'use client';

import React, { useEffect, useState } from 'react';
import { useRouter } from 'next/navigation';
import { ScrollArea } from '@/components/ui/scroll-area';
import AnalyticsGraphs from './AnalyticsGraphs';
import { AuthApi, ProjectApi, TicketApi } from '@game-guild/apiclient/api';
import { getSession } from 'next-auth/react';
import { Api } from '@game-guild/apiclient/models';

// eslint-disable-next-line @typescript-eslint/explicit-function-return-type
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
// eslint-disable-next-line @typescript-eslint/explicit-function-return-type
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
// eslint-disable-next-line @typescript-eslint/explicit-function-return-type
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
      Submitted by: {ticket.owner.username}
    </p>
  </div>
);

// TicketGrid component
// eslint-disable-next-line @typescript-eslint/explicit-function-return-type
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
// eslint-disable-next-line @typescript-eslint/explicit-function-return-type
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
// eslint-disable-next-line @typescript-eslint/explicit-function-return-type
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

const apiUser = new AuthApi({
  basePath: process.env.NEXT_PUBLIC_API_URL,
});

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
  const [isSignedIn, setSignedIn] = useState(false);
  const [videos] = useState(initialVideos);
  const router = useRouter();

  // Define the handleTicketClick function once
  // eslint-disable-next-line @typescript-eslint/explicit-function-return-type
  const handleTicketClick = (ticket) => {
    localStorage.setItem('selectedTicket', JSON.stringify(ticket));
    router.push(`/gtl/owner/Tickets/`);
  };

  // eslint-disable-next-line @typescript-eslint/explicit-function-return-type
  const fetchData = async () => {
    const session = await getSession();
    if (Boolean(session) && Boolean(session?.user)) {
      setSignedIn(true);

      const procjects =
        await apiProject.getManyBaseProjectControllerProjectEntity(
          {},
          {
            headers: {
              Authorization: `Bearer ${session?.user?.accessToken}`,
            },
          },
        );

      if (procjects && Array.isArray(procjects.body)) {
        const ownedProjects: Api.ProjectEntity[] = []; // Temporary array for owned projects
        const updatedTickets: Api.TicketEntity[] = []; // Temporary array for tickets

        // Filter and collect the owned projects
        procjects.body.forEach((project: Api.ProjectEntity) => {
          if (project.owner.id === session?.user?.id) {
            ownedProjects.push(project); // Push to temporary array
          }
        });
        for (const project of ownedProjects) {
          if (project.tickets && Array.isArray(project.tickets)) {
            for (const ticket of project.tickets) {
              const pulledTicket =
                await apiTicket.getOneBaseTicketControllerTicketEntity(
                  { id: ticket.id },
                  {
                    headers: {
                      Authorization: `Bearer ${session?.user?.accessToken}`,
                    },
                  },
                );
              updatedTickets.push(pulledTicket.body as Api.TicketEntity); // Push tickets to temporary array
            }
          }
        }

        // Update state with the new arrays
        setProjects(ownedProjects); // Set the new array for projects
        setTickets(updatedTickets); // Set the new array for tickets
      }
    } else {
      setSignedIn(false);
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

  // eslint-disable-next-line @typescript-eslint/explicit-function-return-type
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

          {/* Check if there are no projects */}
          {projects.length === 0 ? (
            <div className="p-6 text-center">
              {isSignedIn ? (
                <>
                  <p className="text-lg mb-4">
                    It seems that you do not have any projects, please upload
                    one.
                  </p>
                  <button
                    onClick={(e) => {
                      e.preventDefault();
                    }}
                    className="bg-pink-500 hover:bg-pink-600 text-white font-semibold px-4 py-2 rounded"
                  >
                    Upload Project
                  </button>
                  <p className="text-lg mb-4">
                    Work in progress! Please help us to implement this feature.
                    Go to our discord or github to contribute.
                  </p>
                </>
              ) : (
                <>
                  <p className="text-lg mb-4">
                    It seems like you are not signed in, please sign in here.
                  </p>
                  <button
                    onClick={() => {
                      // Handle the sign-in logic here (e.g., redirect to sign-in page)
                      router.push('/connect'); // Example of pushing to a sign-in route
                    }}
                    className="bg-blue-500 hover:bg-blue-600 text-white font-semibold px-4 py-2 rounded"
                  >
                    Sign In
                  </button>
                </>
              )}
            </div>
          ) : (
            // If there are projects, render the content as usual
            <div className="p-6">{renderContent()}</div>
          )}
        </div>
      </main>
    </div>
  );
}
