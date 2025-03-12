'use client';

import React, { useEffect, useState } from 'react';
import { useRouter } from 'next/navigation';
import Link from 'next/link';
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/chess/ui/card';
import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from '@/components/chess/ui/table';
import { Button } from '@/components/chess/ui/button';
import { Skeleton } from '@/components/chess/ui/skeleton';
import { Alert, AlertDescription, AlertTitle } from '@/components/chess/ui/alert';
import { Badge } from '@/components/chess/ui/badge';
import { formatDistanceToNow } from 'date-fns';
import { AlertTriangle, Clock, Eye, Trophy } from 'lucide-react';
import { Api, CompetitionsApi } from '@game-guild/apiclient';
import { getSession } from 'next-auth/react';
import { GetSessionReturnType } from '@/config/auth.config';
import MatchSearchResponseDto = Api.MatchSearchResponseDto;
import MatchSearchRequestDto = Api.MatchSearchRequestDto;

interface ChessMatchResultDto {
  id: string;
  players: string[];
  moves: string[];
  winner: string;
  draw: boolean;
  result: string;
  reason: string;
  cpuTime: number[];
  finalFen: string;
  eloChange: number[];
  elo: number[];
  createdAt: string;
}

// Mock data for the matches list with valid UCI format moves
const mockMatches: ChessMatchResultDto[] = [
  {
    id: 'match_123456',
    players: ['GrandMaster42', 'DeepBlue2023'],
    moves: ['e2e4', 'e7e5', 'g1f3', 'b8c6', 'f1b5', 'a7a6', 'b5a4', 'g8f6', 'e1g1', 'f8e7'],
    winner: 'GrandMaster42',
    draw: false,
    result: 'WIN',
    reason: 'CHECKMATE',
    cpuTime: [1200, 1500],
    finalFen: '8/8/8/8/8/7P/2R3PK/k7 b - - 0 76',
    eloChange: [15, -15],
    elo: [2865, 2775],
    createdAt: '2023-12-15T14:30:00Z',
  },
  {
    id: 'match_short',
    players: ['Scholar', 'Mate'],
    moves: ['e2e4', 'e7e5', 'd1h5', 'b8c6', 'f1c4', 'g8f6', 'h5f7'],
    winner: 'Scholar',
    draw: false,
    result: 'WIN',
    reason: 'CHECKMATE',
    cpuTime: [500, 600],
    finalFen: 'r1bqkb1r/pppp1Qpp/2n2n/4p3/2B1P3/8/PPPP1PPP/RNB1K1NR b KQkq - 0 4',
    eloChange: [10, -10],
    elo: [2500, 2490],
    createdAt: '2023-12-20T10:15:00Z',
  },
  {
    id: 'match_draw',
    players: ['StaleBot', 'DrawMaster'],
    moves: ['e2e4', 'e7e5', 'g1f3', 'b8c6', 'f1b5', 'a7a6', 'b5a4', 'g8f6', 'e1g1', 'f8e7'],
    winner: '',
    draw: true,
    result: 'DRAW',
    reason: 'STALEMATE',
    cpuTime: [800, 900],
    finalFen: '8/8/8/8/8/k7/p7/K7 w - - 0 50',
    eloChange: [0, 0],
    elo: [2600, 2600],
    createdAt: '2023-12-18T09:45:00Z',
  },
];

export default function MatchesContent() {
  const [matches, setMatches] = React.useState<MatchSearchResponseDto[]>([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);
  const router = useRouter();
  const [accessToken, setAccessToken] = useState('');
  const api = new CompetitionsApi({
    basePath: process.env.NEXT_PUBLIC_API_URL,
  });

  useEffect(() => {
    const fetchMatches = async () => {
      let token = accessToken;

      if (!token) {
        const localSession = (await getSession()) as unknown as GetSessionReturnType;
        if (localSession) {
          token = localSession.user.accessToken;
          setAccessToken(token);
          console.log(token);
          alert(token);
        } else {
          console.error('No session found');
        }
      }

      try {
        setLoading(true);

        const response = await api.competitionControllerFindChessMatchResult(
          {
            pageSize: 100,
            pageId: 0,
          } as MatchSearchRequestDto,
          {
            headers: {
              Authorization: `Bearer ${token}`,
            },
          },
        );

        if (response.status === 401) {
          setError('You are not authorized to view this page. Detailed error: ' + JSON.stringify(response.body));
          // setTimeout(() => {
          //   window.location.href = '/disconnect';
          // }, 1000);
          return;
        }

        if (response.status === 500) {
          setError('Internal server error. Please report this issue to the community. See console for details.');
          console.error(JSON.stringify(response.body));
          return;
        }

        const data = response.body as MatchSearchResponseDto[];
        console.log(data);
        setMatches(data);
      } catch (err) {
        console.error('Error fetching matches:', err);
        setError('Failed to load matches. Please try again later.');
      } finally {
        setLoading(false);
      }
    };

    fetchMatches();
  }, []);

  const handleViewReplay = (matchId: string) => {
    window.href = `/replay?matchid=${matchId}`;
  };

  // Format CPU time
  const formatCpuTime = (ms: number) => {
    if (ms < 1000) return `${ms}ms`;
    return `${(ms / 1000).toFixed(2)}s`;
  };

  return (
    <div className="space-y-6">
      <Card>
        <CardHeader>
          <CardTitle>Recent Chess Matches</CardTitle>
          <CardDescription>View and replay recent matches between chess bots</CardDescription>
        </CardHeader>
        <CardContent>
          {error && (
            <Alert variant="destructive" className="mb-4">
              <AlertTriangle className="h-4 w-4" />
              <AlertTitle>Error</AlertTitle>
              <AlertDescription>{error}</AlertDescription>
            </Alert>
          )}

          {loading ? (
            <div className="space-y-2">
              {Array.from({ length: 3 }).map((_, index) => (
                <div key={index} className="flex items-center space-x-4">
                  <Skeleton className="h-12 w-full" />
                </div>
              ))}
            </div>
          ) : (
            <Table>
              <TableHeader>
                <TableRow>
                  <TableHead>Players</TableHead>
                  <TableHead>Result</TableHead>
                  <TableHead>Date</TableHead>
                  <TableHead>CPU Time</TableHead>
                  <TableHead className="text-right">Actions</TableHead>
                </TableRow>
              </TableHeader>
              <TableBody>
                {matches.map((match) => (
                  <TableRow key={match.id}>
                    <TableCell>
                      <Link
                        href={`/replay?matchid=${match.id}`}
                        className="font-medium hover:underline cursor-pointer"
                        title={`View replay of ${match.players[0]} vs ${match.players[1]}`}
                      >
                        {match.players[0]} vs {match.players[1]}
                      </Link>
                      {/*<div className="text-sm text-muted-foreground">{match.moves.length} moves</div>*/}
                    </TableCell>
                    <TableCell>
                      {match.winner == null ? (
                        <Badge variant="secondary">Draw</Badge>
                      ) : (
                        <div className="flex flex-col gap-1">
                          <Badge className="w-fit bg-green-500 flex items-center gap-1">
                            <Trophy className="h-3 w-3" />
                            {match.winner === 'Player1' ? match.players[0] : match.players[1]}
                          </Badge>
                          {/*<span className="text-xs text-muted-foreground">{match.reason}</span>*/}
                        </div>
                      )}
                    </TableCell>
                    <TableCell>{formatDistanceToNow(new Date(match.createdAt), { addSuffix: true })}</TableCell>
                    <TableCell>
                      <div className="flex items-center gap-1">
                        <Clock className="h-3 w-3" />
                        {/*<span>{formatCpuTime(match.cpuTime[0])}</span>*/}
                        <span>/</span>
                        {/*<span>{formatCpuTime(match.cpuTime[1])}</span>*/}
                      </div>
                    </TableCell>
                    <TableCell className="text-right">
                      <Button variant="outline" size="sm" onClick={() => handleViewReplay(match.id)} className="flex items-center gap-1">
                        <Eye className="h-4 w-4" />
                        Replay
                      </Button>
                    </TableCell>
                  </TableRow>
                ))}
              </TableBody>
            </Table>
          )}
        </CardContent>
      </Card>
    </div>
  );
}
