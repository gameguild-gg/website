'use client';

import React, { useEffect, useState } from 'react';
import { ScrollArea } from '@/components/ui/scroll-area';
import { Button } from '@/components/ui/button';
import { Input } from '@/components/ui/input';
import { getSession } from 'next-auth/react';
import { Textarea } from '@/components/ui/textarea';
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from '@/components/ui/select';
import { ChevronLeft, ChevronRight, Ticket } from 'lucide-react';
import { AuthApi, TicketApi } from '@game-guild/apiclient/api';

const gameCover = '/assets/images/game_cover.png';

type Game = {
  id: string;
  title: string;
  image: string;
  genre: string;
  Gradble: boolean;
};

enum TicketStatus {
  OPEN = 'OPEN',
  IN_PROGRESS = 'IN_PROGRESS',
  CLOSED = 'CLOSED',
  RESOLVED = 'RESOLVED',
}

enum TicketPriority {
  LOW = 'LOW',
  MEDIUM = 'MEDIUM',
  HIGH = 'HIGH',
  CRITICAL = 'CRITICAL',
}

const Games: Game[] = [
  {
    id: 'c86288fc-7a41-48b1-9872-9f07c0035f5c',
    title: 'Game 1',
    image: gameCover,
    genre: 'Action',
    Gradble: false,
  },
  {
    id: '50c7bf71-c58a-4ab9-aa32-47f4e98086d6',
    title: 'Game 2',
    image: gameCover,
    genre: 'Adventure',
    Gradble: false,
  },
  {
    id: 'd9e6f9fc-4c41-47c1-9872-9f07c0035f7e',
    title: 'Game 3',
    image: gameCover,
    genre: 'RPG',
    Gradble: false,
  },
  {
    id: '50c7bf72-c58a-4ab9-aa32-47f4e98086d6',
    title: 'Game 4',
    image: gameCover,
    genre: 'Strategy',
    Gradble: false,
  },
  {
    id: '2c7bf71a-1a2b-4ab9-aa32-3f4e98076b9f',
    title: 'Game 5',
    image: gameCover,
    genre: 'Action',
    Gradble: false,
  },
  {
    id: 'f7c5bf71-28a7-4b21-9a4c-93a76f980c12',
    title: 'Game 6',
    image: gameCover,
    genre: 'Adventure',
    Gradble: false,
  },
  {
    id: '07f1f8ac-1b41-38b1-7c72-1f01c0134b8d',
    title: 'Game 7',
    image: gameCover,
    genre: 'RPG',
    Gradble: false,
  },
  {
    id: '2f6ff9ec-5c41-46c1-9382-9a08c0131a7e',
    title: 'Game 8',
    image: gameCover,
    genre: 'Strategy',
    Gradble: false,
  },
  {
    id: 'c87bf51c-9a5c-4c31-8b92-9f07f0072f1b',
    title: 'Game 9',
    image: gameCover,
    genre: 'Action',
    Gradble: false,
  },
  {
    id: '7b17f8d3-6c41-41b2-7c12-1f01c0011a6f',
    title: 'Game 10',
    image: gameCover,
    genre: 'Adventure',
    Gradble: false,
  },
  {
    id: 'a7f5e9ff-8c11-4ac1-8371-2f02f0134c5a',
    title: 'Game 11',
    image: gameCover,
    genre: 'RPG',
    Gradble: false,
  },
  {
    id: '57f1f8fc-6b41-49b1-9872-4c08f0133a5c',
    title: 'Game 12',
    image: gameCover,
    genre: 'Strategy',
    Gradble: false,
  },
  {
    id: '5c7bf61c-4b3c-47c1-9a02-3c01e0015a7d',
    title: 'Game 13',
    image: gameCover,
    genre: 'Strategy',
    Gradble: true,
  },
  {
    id: 'c87ef51c-9a3f-4a31-8a32-9f07e0123b7c',
    title: 'Game 14',
    image: gameCover,
    genre: 'Strategy',
    Gradble: true,
  },
  {
    id: 'e1c5bf71-9a72-4ab2-9b4a-9c01c0138a5d',
    title: 'Game 15',
    image: gameCover,
    genre: 'Strategy',
    Gradble: true,
  },
  {
    id: 'a7f5ef0c-5c42-4ab3-8a82-4f07b0121c8e',
    title: 'Game 16',
    image: gameCover,
    genre: 'Strategy',
    Gradble: true,
  },
];

const genres = ['All', 'Action', 'Adventure', 'RPG', 'Strategy'];

const apiTicket = new TicketApi({
  basePath: process.env.NEXT_PUBLIC_API_URL,
});
const apiUser = new AuthApi({
  basePath: process.env.NEXT_PUBLIC_API_URL,
});

const gameIds = Games.map((game) => ({ gameName: game.title, id: game.id }));

export default function GameMarketplace() {
  const [selectedGenre, setSelectedGenre] = useState('All');
  const [startIndex, setStartIndex] = useState(0);
  const [gradableGames, setGradableGames] = useState<Game[]>([]);
  const [nonGradableGames, setNonGradableGames] = useState<Game[]>([]);
  const [showTicketForm, setShowTicketForm] = useState(false);
  const [ticketTitle, setTicketTitle] = useState('');
  const [ticketDescription, setTicketDescription] = useState('');
  const [ticketStatus, setTicketStatus] = useState<TicketStatus>(
    TicketStatus.OPEN,
  );
  const [ticketPriority, setTicketPriority] = useState<TicketPriority>(
    TicketPriority.LOW,
  );
  const [selectedGameId, setSelectedGameId] = useState<string>('');

  const filteredGames =
    selectedGenre === 'All'
      ? nonGradableGames
      : nonGradableGames.filter((game) => game.genre === selectedGenre);

  const handlePrev = () => {
    setStartIndex((prevIndex) =>
      prevIndex === 0 ? gradableGames.length - 1 : prevIndex - 1,
    );
  };

  const handleNext = () => {
    setStartIndex((prevIndex) => (prevIndex + 1) % gradableGames.length);
  };

  const visibleGames = [
    gradableGames[startIndex],
    gradableGames[(startIndex + 1) % gradableGames.length],
    gradableGames[(startIndex + 2) % gradableGames.length],
  ].filter(Boolean);

  useEffect(() => {
    const gradable: Game[] = [];
    const nonGradable: Game[] = [];
    Games.forEach((game) => {
      if (game.Gradble) {
        gradable.push(game);
      } else {
        nonGradable.push(game);
      }
    });
    setGradableGames(gradable);
    setNonGradableGames(nonGradable);
  }, []);

  const handleSubmitTicket = async () => {
    const session = await getSession();
    const currentUser = await apiUser.authControllerGetCurrentUser({
      headers: {
        Authorization: `Bearer ${session?.user?.accessToken}`,
      },
    });
    const submitedTicket =
      await apiTicket.createOneBaseTicketControllerTicketEntity(
        {
          title: ticketTitle,
          owner: currentUser.body,
          status: ticketStatus,
          priority: ticketPriority,
          description: ticketDescription,
          projectId: selectedGameId,
        },
        {
          headers: {
            Authorization: `Bearer ${session?.user?.accessToken}`,
          },
        },
      );
    setTicketTitle('');
    setTicketDescription('');
    setTicketStatus(TicketStatus.OPEN);
    setTicketPriority(TicketPriority.LOW);
    setSelectedGameId('');
    setShowTicketForm(false);
  };

  return (
    <div className="flex min-h-screen bg-black text-white">
      <div className="w-64 fixed h-full overflow-auto border-r border-gray-800">
        <div className="p-6">
          <h2 className="text-xl font-semibold mb-4">Genres</h2>
          {genres.map((genre) => (
            <button
              key={genre}
              onClick={() => setSelectedGenre(genre)}
              className={`block w-full text-left py-2 px-4 rounded ${
                selectedGenre === genre ? 'bg-gray-800' : 'hover:bg-gray-900'
              }`}
            >
              {genre}
            </button>
          ))}
          <Button
            className="mt-4 w-full"
            onClick={() => setShowTicketForm(true)}
          >
            <Ticket className="mr-2 h-4 w-4" />
            Submit Ticket
          </Button>
        </div>
      </div>

      <div className="flex-1 ml-64 p-8">
        {showTicketForm ? (
          <div className="bg-gray-800 p-6 rounded-lg">
            <h2 className="text-2xl font-semibold mb-4">Submit a Ticket</h2>
            <form
              onSubmit={(e) => {
                e.preventDefault();
                //handleSubmitTicket();
                // the ticket submition works this is here so people cant spam it
              }}
              className="space-y-4"
            >
              <div>
                <label
                  htmlFor="title"
                  className="block text-sm font-medium text-gray-400"
                >
                  Title (75 characters max)
                </label>
                <Input
                  id="title"
                  value={ticketTitle}
                  onChange={(e) => setTicketTitle(e.target.value.slice(0, 75))}
                  required
                  maxLength={75}
                  className="bg-gray-700 text-white"
                />
                <p className="text-sm text-gray-400 mt-1">
                  {ticketTitle.length}/75 characters
                </p>
              </div>
              <div>
                <label
                  htmlFor="description"
                  className="block text-sm font-medium text-gray-400"
                >
                  Description
                </label>
                <Textarea
                  id="description"
                  value={ticketDescription}
                  onChange={(e) => setTicketDescription(e.target.value)}
                  required
                  className="bg-gray-700 text-white"
                />
              </div>
              <div>
                <label
                  htmlFor="status"
                  className="block text-sm font-medium text-gray-400"
                >
                  Status
                </label>
                <Select
                  value={ticketStatus}
                  onValueChange={(value) =>
                    setTicketStatus(value as TicketStatus)
                  }
                  required
                >
                  <SelectTrigger className="bg-gray-700 text-white">
                    <SelectValue placeholder="Select status" />
                  </SelectTrigger>
                  <SelectContent className="bg-gray-700 text-white">
                    {Object.values(TicketStatus).map((status) => (
                      <SelectItem key={status} value={status}>
                        {status}
                      </SelectItem>
                    ))}
                  </SelectContent>
                </Select>
              </div>
              <div>
                <label
                  htmlFor="priority"
                  className="block text-sm font-medium text-gray-400"
                >
                  Priority
                </label>
                <Select
                  value={ticketPriority}
                  onValueChange={(value) =>
                    setTicketPriority(value as TicketPriority)
                  }
                  required
                >
                  <SelectTrigger className="bg-gray-700 text-white">
                    <SelectValue placeholder="Select priority" />
                  </SelectTrigger>
                  <SelectContent className="bg-gray-700 text-white">
                    {Object.values(TicketPriority).map((priority) => (
                      <SelectItem key={priority} value={priority}>
                        {priority}
                      </SelectItem>
                    ))}
                  </SelectContent>
                </Select>
              </div>
              <div>
                <label
                  htmlFor="gameId"
                  className="block text-sm font-medium text-gray-400"
                >
                  Game
                </label>
                <Select
                  value={selectedGameId ? selectedGameId.toString() : undefined}
                  onValueChange={(value) => setSelectedGameId(String(value))}
                  required
                >
                  <SelectTrigger className="bg-gray-700 text-white">
                    <SelectValue placeholder="Select game" />
                  </SelectTrigger>
                  <SelectContent className="bg-gray-700 text-white">
                    <ScrollArea className="h-[200px]">
                      {gameIds.map((game) => (
                        <SelectItem key={game.id} value={game.id.toString()}>
                          {game.gameName}
                        </SelectItem>
                      ))}
                    </ScrollArea>
                  </SelectContent>
                </Select>
              </div>
              <div className="flex justify-between items-center">
                <Button type="submit">Submit Ticket</Button>
                <Button
                  variant="secondary"
                  onClick={() => setShowTicketForm(false)}
                >
                  Cancel
                </Button>
              </div>
            </form>
          </div>
        ) : (
          <>
            <div className="mb-8">
              <h2 className="text-2xl font-semibold mb-4">Top Games</h2>
              <div className="relative px-12">
                <Button
                  variant="ghost"
                  size="icon"
                  className="absolute left-0 top-1/2 transform -translate-y-1/2 z-10 text-gray-400 hover:text-white hover:bg-gray-800 transition-colors"
                  onClick={handlePrev}
                >
                  <ChevronLeft className="h-6 w-6" />
                </Button>
                <ScrollArea className="w-full overflow-hidden">
                  <div className="flex justify-center space-x-4 p-4">
                    {visibleGames.map((game, index) => (
                      <div key={index} className="w-[300px] shrink-0">
                        <img
                          src={game.image}
                          alt={game.title}
                          className="w-full h-[200px] object-cover rounded-md"
                        />
                        <p className="mt-2 text-center">{game.title}</p>
                      </div>
                    ))}
                  </div>
                </ScrollArea>
                <Button
                  variant="ghost"
                  size="icon"
                  className="absolute right-0 top-1/2 transform -translate-y-1/2 z-10 text-gray-400 hover:text-white hover:bg-gray-800 transition-colors"
                  onClick={handleNext}
                >
                  <ChevronRight className="h-6 w-6" />
                </Button>
              </div>
            </div>

            <div>
              <h2 className="text-2xl font-semibold mb-4">Other Games</h2>
              <div className="grid grid-cols-1 sm:grid-cols-2 md:grid-cols-3 lg:grid-cols-4 gap-6">
                {filteredGames.map((game) => (
                  <div
                    key={game.id}
                    className="bg-gray-800 rounded-lg overflow-hidden"
                  >
                    <img
                      src={game.image}
                      alt={game.title}
                      className="w-full h-[150px] object-cover"
                    />
                    <div className="p-4">
                      <h3 className="font-semibold">{game.title}</h3>
                      <p className="text-sm text-gray-400">{game.genre}</p>
                    </div>
                  </div>
                ))}
              </div>
            </div>
          </>
        )}
      </div>
    </div>
  );
}
