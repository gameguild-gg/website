'use client';

import { useEffect, useState } from 'react';
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/chess/ui/card';
import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from '@/components/chess/ui/table';
import { Medal, Trophy } from 'lucide-react';
import { Skeleton } from '@/components/chess/ui/skeleton';
import { Alert, AlertDescription, AlertTitle } from '@/components/chess/ui/alert';

interface ChessLeaderboardResponseEntryDto {
  username: string;
  elo: number;
}

export default function LeaderboardContent() {
  const [leaderboardData, setLeaderboardData] = useState<ChessLeaderboardResponseEntryDto[]>([]);
  const [isLoading, setIsLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    const fetchLeaderboardData = async () => {
      try {
        setIsLoading(true);
        const response = await fetch('/api/leaderboard');

        if (!response.ok) {
          throw new Error('Failed to fetch leaderboard data');
        }

        const data = await response.json();
        setLeaderboardData(data);
        setError(null);
      } catch (err) {
        setError('Error loading leaderboard data. Please try again later.');
        console.error(err);
      } finally {
        setIsLoading(false);
      }
    };

    fetchLeaderboardData();
  }, []);

  // Function to render rank badges for top 3 positions
  const renderRankBadge = (rank: number) => {
    if (rank === 1) {
      return <Trophy className="h-5 w-5 text-yellow-500" />;
    } else if (rank === 2) {
      return <Medal className="h-5 w-5 text-gray-400" />;
    } else if (rank === 3) {
      return <Medal className="h-5 w-5 text-amber-700" />;
    }
    return rank;
  };

  return (
    <div className="space-y-6">
      <Card>
        <CardHeader>
          <CardTitle>Chess Bot Rankings</CardTitle>
          <CardDescription>Current ELO ratings of all competing bots</CardDescription>
        </CardHeader>
        <CardContent>
          {error && (
            <Alert variant="destructive" className="mb-4">
              <AlertTitle>Error</AlertTitle>
              <AlertDescription>{error}</AlertDescription>
            </Alert>
          )}

          {isLoading ? (
            <div className="space-y-2">
              {Array.from({ length: 5 }).map((_, index) => (
                <div key={index} className="flex items-center space-x-4">
                  <Skeleton className="h-12 w-full" />
                </div>
              ))}
            </div>
          ) : (
            <Table>
              <TableHeader>
                <TableRow>
                  <TableHead className="w-16">Rank</TableHead>
                  <TableHead>Username</TableHead>
                  <TableHead className="text-right">ELO Rating</TableHead>
                </TableRow>
              </TableHeader>
              <TableBody>
                {leaderboardData
                  .sort((a, b) => b.elo - a.elo)
                  .map((entry, index) => (
                    <TableRow key={entry.username} className={index < 3 ? 'font-medium' : ''}>
                      <TableCell className="flex items-center justify-center">
                        <div className="flex items-center justify-center w-8 h-8">{renderRankBadge(index + 1)}</div>
                      </TableCell>
                      <TableCell>{entry.username}</TableCell>
                      <TableCell className="text-right">{entry.elo}</TableCell>
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
