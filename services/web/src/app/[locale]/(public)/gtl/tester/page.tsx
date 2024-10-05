'use client';

import React, { useState, useEffect } from 'react';
import { ScrollArea } from '@/components/ui/scroll-area';
import { Button } from '@/components/ui/button';
import { Input } from '@/components/ui/input';
import { Textarea } from '@/components/ui/textarea';
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from '@/components/ui/select';
import { ChevronLeft, ChevronRight, Ticket } from 'lucide-react';

const gameCover = '/assets/images/game_cover.png';

type Game = {
  id: number;
  title: string;
  image: string;
  genre: string;
  Gradble: boolean;
};

const Games: Game[] = [
  {
    id: 1,
    title: 'Game 1',
    image: gameCover,
    genre: 'Action',
    Gradble: false,
  },
  {
    id: 2,
    title: 'Game 2',
    image: gameCover,
    genre: 'Adventure',
    Gradble: false,
  },
  {
    id: 3,
    title: 'Game 3',
    image: gameCover,
    genre: 'RPG',
    Gradble: false,
  },
  {
    id: 4,
    title: 'Game 4',
    image: gameCover,
    genre: 'Strategy',
    Gradble: false,
  },
  {
    id: 5,
    title: 'Game 5',
    image: gameCover,
    genre: 'Action',
    Gradble: false,
  },
  {
    id: 6,
    title: 'Game 6',
    image: gameCover,
    genre: 'Adventure',
    Gradble: false,
  },
  {
    id: 7,
    title: 'Game 7',
    image: gameCover,
    genre: 'RPG',
    Gradble: false,
  },
  {
    id: 8,
    title: 'Game 8',
    image: gameCover,
    genre: 'Strategy',
    Gradble: false,
  },
  {
    id: 9,
    title: 'Game 9',
    image: gameCover,
    genre: 'Action',
    Gradble: false,
  },
  {
    id: 10,
    title: 'Game 10',
    image: gameCover,
    genre: 'Adventure',
    Gradble: false,
  },
  {
    id: 11,
    title: 'Game 11',
    image: gameCover,
    genre: 'RPG',
    Gradble: false,
  },
  {
    id: 12,
    title: 'Game 12',
    image: gameCover,
    genre: 'Strategy',
    Gradble: false,
  },
  {
    id: 13,
    title: 'Game 13',
    image: gameCover,
    genre: 'Strategy',
    Gradble: true,
  },
  {
    id: 14,
    title: 'Game 14',
    image: gameCover,
    genre: 'Strategy',
    Gradble: true,
  },
  {
    id: 15,
    title: 'Game 15',
    image: gameCover,
    genre: 'Strategy',
    Gradble: true,
  },
  {
    id: 16,
    title: 'Game 16',
    image: gameCover,
    genre: 'Strategy',
    Gradble: true,
  },
];

const genres = ['All', 'Action', 'Adventure', 'RPG', 'Strategy'];

export default function GameMarketplace() {
  const [selectedGenre, setSelectedGenre] = useState('All');
  const [startIndex, setStartIndex] = useState(0);
  const [gradableGames, setGradableGames] = useState<Game[]>([]);
  const [nonGradableGames, setNonGradableGames] = useState<Game[]>([]);
  const [showTicketForm, setShowTicketForm] = useState(false);
  const [ticketTitle, setTicketTitle] = useState('');
  const [ticketDescription, setTicketDescription] = useState('');
  const [ticketStatus, setTicketStatus] = useState('');
  const [ticketPriority, setTicketPriority] = useState('');

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

  const handleSubmitTicket = (e: React.FormEvent) => {
    e.preventDefault();
    console.log({
      ticketTitle,
      ticketDescription,
      ticketStatus,
      ticketPriority,
    });
    setTicketTitle('');
    setTicketDescription('');
    setTicketStatus('');
    setTicketPriority('');
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
            <form onSubmit={handleSubmitTicket} className="space-y-4">
              <div>
                <label
                  htmlFor="title"
                  className="block text-sm font-medium text-gray-400"
                >
                  Title
                </label>
                <Input
                  id="title"
                  value={ticketTitle}
                  onChange={(e) => setTicketTitle(e.target.value)}
                  required
                />
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
                />
              </div>
              <div>
                <label
                  htmlFor="status"
                  className="block text-sm font-medium text-gray-400"
                >
                  Status
                </label>
                <Select onValueChange={setTicketStatus} required>
                  <SelectTrigger>
                    <SelectValue placeholder="Select status" />
                  </SelectTrigger>
                  <SelectContent>
                    <SelectItem value="open">Open</SelectItem>
                    <SelectItem value="in-progress">In Progress</SelectItem>
                    <SelectItem value="closed">Closed</SelectItem>
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
                <Select onValueChange={setTicketPriority} required>
                  <SelectTrigger>
                    <SelectValue placeholder="Select priority" />
                  </SelectTrigger>
                  <SelectContent>
                    <SelectItem value="low">Low</SelectItem>
                    <SelectItem value="medium">Medium</SelectItem>
                    <SelectItem value="high">High</SelectItem>
                  </SelectContent>
                </Select>
              </div>
              <Button type="submit">Submit Ticket</Button>
              <Button
                variant="outline"
                onClick={() => setShowTicketForm(false)}
              >
                Cancel
              </Button>
            </form>
          </div>
        ) : (
          <>
            <div className="mb-8">
              <h2 className="text-2xl font-semibold mb-4">Gradable Games</h2>
              <div className="relative px-12">
                <Button
                  variant="outline"
                  size="icon"
                  className="absolute left-0 top-1/2 transform -translate-y-1/2 z-10"
                  onClick={handlePrev}
                >
                  <ChevronLeft className="h-4 w-4" />
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
                  variant="outline"
                  size="icon"
                  className="absolute right-0 top-1/2 transform -translate-y-1/2 z-10"
                  onClick={handleNext}
                >
                  <ChevronRight className="h-4 w-4" />
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
