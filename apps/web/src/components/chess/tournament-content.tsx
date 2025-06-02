'use client';

import { useEffect, useState } from 'react';
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/chess/ui/card';
import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from '@/components/chess/ui/table';
import { Badge } from '@/components/chess/ui/badge';
import { Button } from '@/components/chess/ui/button';
import { Alert, AlertDescription, AlertTitle } from '@/components/chess/ui/alert';
import { Skeleton } from '@/components/chess/ui/skeleton';
import { CalendarIcon, Clock, Info, Swords, TrophyIcon } from 'lucide-react';
import Link from 'next/link';

interface TournamentStanding {
  position: number;
  botName: string;
  author: string;
  elo: number;
  wins: number;
  losses: number;
  draws: number;
}

interface NotableMatch {
  id: string;
  white: string;
  black: string;
  result: string;
  round: string;
}

interface TournamentData {
  id: string;
  date: string;
  status: string;
  participants: number;
  rounds: number;
  winner: {
    name: string;
    author: string;
    elo: number;
  };
  standings: TournamentStanding[];
  notable_matches: NotableMatch[];
  next_tournament: {
    date: string;
    registration_deadline: string;
    estimated_participants: number;
  };
}

export default function TournamentContent() {
  const [isLoading, setIsLoading] = useState(true);
  const [tournamentData, setTournamentData] = useState<TournamentData | null>(null);
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    const fetchTournamentData = async () => {
      try {
        const response = await fetch('/api/tournament');
        if (!response.ok) {
          throw new Error('Failed to fetch tournament data');
        }
        const data = await response.json();
        setTournamentData(data.tournament);
      } catch (err) {
        setError('An error occurred while fetching tournament data. Please try again later.');
        console.error(err);
      } finally {
        setIsLoading(false);
      }
    };

    fetchTournamentData();
  }, []);

  const formatDate = (dateStr: string) => {
    const options: Intl.DateTimeFormatOptions = {
      year: 'numeric',
      month: 'long',
      day: 'numeric',
    };
    return new Date(dateStr).toLocaleDateString(undefined, options);
  };

  const timeUntilNextTournament = () => {
    if (!tournamentData) return '';

    const nextDate = new Date(tournamentData.next_tournament.date);
    const now = new Date();
    const diffTime = Math.abs(nextDate.getTime() - now.getTime());
    const diffDays = Math.floor(diffTime / (1000 * 60 * 60 * 24));
    const diffHours = Math.floor((diffTime % (1000 * 60 * 60 * 24)) / (1000 * 60 * 60));

    if (diffDays > 0) {
      return `${diffDays} day${diffDays > 1 ? 's' : ''}`;
    }
    return `${diffHours} hour${diffHours > 1 ? 's' : ''}`;
  };

  return (
    <div className="space-y-6">
      {error && (
        <Alert variant="destructive">
          <AlertTitle>Error</AlertTitle>
          <AlertDescription>{error}</AlertDescription>
        </Alert>
      )}

      <Card>
        <CardHeader className="pb-3">
          <CardTitle className="flex items-center justify-between">
            <div className="flex items-center gap-2">
              <CalendarIcon className="h-5 w-5" />
              <span>Tournament Schedule</span>
            </div>
            <Badge variant="outline" className="font-normal">
              Daily
            </Badge>
          </CardTitle>
          <CardDescription>Tournaments run automatically every day at 12:00 UTC</CardDescription>
        </CardHeader>
        <CardContent>
          {isLoading ? (
            <div className="space-y-4">
              <Skeleton className="h-16 w-full" />
              <Skeleton className="h-16 w-full" />
            </div>
          ) : tournamentData ? (
            <div className="space-y-4">
              <div className="rounded-md border p-4">
                <div className="flex flex-col sm:flex-row justify-between gap-4">
                  <div>
                    <h3 className="font-semibold">Next Tournament</h3>
                    <p className="text-sm text-muted-foreground">
                      {formatDate(tournamentData.next_tournament.date)} (in {timeUntilNextTournament()})
                    </p>
                    <p className="text-sm text-muted-foreground mt-1">
                      Registration deadline:{' '}
                      {new Date(tournamentData.next_tournament.registration_deadline).toLocaleTimeString([], {
                        hour: '2-digit',
                        minute: '2-digit',
                      })}{' '}
                      today
                    </p>
                  </div>
                  <div className="flex gap-2">
                    <Button size="sm">Register Bot</Button>
                    <Button size="sm" variant="outline">
                      <Info className="h-4 w-4 mr-1" />
                      Tournament Rules
                    </Button>
                  </div>
                </div>
                <div className="mt-2 text-sm">
                  <p className="flex items-center">
                    <Clock className="h-4 w-4 mr-1 inline text-muted-foreground" />
                    <span>Estimated participants: {tournamentData.next_tournament.estimated_participants}</span>
                  </p>
                </div>
              </div>

              <div className="flex items-center justify-between">
                <p className="text-sm text-muted-foreground">
                  Bots are paired using a Swiss tournament system. The winner is determined after {tournamentData.rounds} rounds.
                </p>
              </div>
            </div>
          ) : null}
        </CardContent>
      </Card>

      <Card>
        <CardHeader>
          <CardTitle className="flex items-center gap-2">
            <TrophyIcon className="h-5 w-5" />
            <span>Latest Tournament Results</span>
          </CardTitle>
          <CardDescription>
            {isLoading ? (
              <Skeleton className="h-4 w-48" />
            ) : tournamentData ? (
              <>
                Completed on {formatDate(tournamentData.date)} with {tournamentData.participants} participants
              </>
            ) : null}
          </CardDescription>
        </CardHeader>
        <CardContent>
          {isLoading ? (
            <div className="space-y-4">
              <Skeleton className="h-32 w-full" />
              <Skeleton className="h-64 w-full" />
            </div>
          ) : tournamentData ? (
            <div className="space-y-6">
              <div className="rounded-lg bg-muted p-4 text-center sm:text-left sm:flex items-center justify-between">
                <div className="flex-1">
                  <div className="text-lg font-semibold mb-1">Tournament Winner</div>
                  <div className="text-2xl font-bold flex justify-center sm:justify-start items-center gap-2">
                    <TrophyIcon className="h-6 w-6 text-yellow-500" />
                    {tournamentData.winner.name}
                  </div>
                  <div className="text-sm text-muted-foreground mt-1">
                    by {tournamentData.winner.author} (ELO: {tournamentData.winner.elo})
                  </div>
                </div>
                <div className="mt-4 sm:mt-0">
                  <Button asChild>
                    <Link href={`/submit`}>Submit Your Bot</Link>
                  </Button>
                </div>
              </div>

              <div>
                <h3 className="text-lg font-semibold mb-3">Final Standings</h3>
                <div className="border rounded-md overflow-hidden">
                  <Table>
                    <TableHeader>
                      <TableRow>
                        <TableHead className="w-12">Pos</TableHead>
                        <TableHead>Bot</TableHead>
                        <TableHead>Author</TableHead>
                        <TableHead className="text-right">ELO</TableHead>
                        <TableHead className="text-right">W-L-D</TableHead>
                      </TableRow>
                    </TableHeader>
                    <TableBody>
                      {tournamentData.standings.map((standing) => (
                        <TableRow key={standing.position}>
                          <TableCell className="font-medium">
                            {standing.position === 1 ? (
                              <span className="flex items-center">
                                1 <TrophyIcon className="h-4 w-4 text-yellow-500 ml-1" />
                              </span>
                            ) : (
                              standing.position
                            )}
                          </TableCell>
                          <TableCell>{standing.botName}</TableCell>
                          <TableCell>{standing.author}</TableCell>
                          <TableCell className="text-right">{standing.elo}</TableCell>
                          <TableCell className="text-right">
                            {standing.wins}-{standing.losses}-{standing.draws}
                          </TableCell>
                        </TableRow>
                      ))}
                    </TableBody>
                  </Table>
                </div>
              </div>

              {tournamentData.notable_matches.length > 0 && (
                <div>
                  <h3 className="text-lg font-semibold mb-3">Notable Matches</h3>
                  <div className="space-y-3">
                    {tournamentData.notable_matches.map((match) => (
                      <div key={match.id} className="flex flex-col sm:flex-row items-start sm:items-center justify-between p-3 border rounded-md">
                        <div>
                          <div className="flex items-center gap-2">
                            <Badge variant="outline">{match.round}</Badge>
                            <Swords className="h-4 w-4 text-muted-foreground" />
                          </div>
                          <div className="mt-2 font-semibold">
                            {match.white} vs {match.black}
                          </div>
                          <div className="text-sm">
                            Result: <span className="font-medium">{match.result}</span>
                          </div>
                        </div>
                        <Button variant="outline" size="sm" className="mt-3 sm:mt-0" asChild>
                          <Link href={`/replay?id=${match.id}`}>Replay Match</Link>
                        </Button>
                      </div>
                    ))}
                  </div>
                </div>
              )}

              <div className="text-center mt-6">
                <Button asChild>
                  <Link href="/matches">View All Tournament Matches</Link>
                </Button>
              </div>
            </div>
          ) : null}
        </CardContent>
      </Card>
    </div>
  );
}
