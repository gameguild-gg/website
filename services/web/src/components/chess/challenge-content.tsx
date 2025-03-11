'use client';

import { useEffect, useState } from 'react';
import { useRouter } from 'next/navigation';
import { Card, CardContent, CardDescription, CardFooter, CardHeader, CardTitle } from '@/components/chess/ui/card';
import { Button } from '@/components/chess/ui/button';
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from '@/components/chess/ui/select';
import { Alert, AlertDescription, AlertTitle } from '@/components/chess/ui/alert';
import { Badge } from '@/components/chess/ui/badge';
import { AlertCircle, ArrowRight, CheckCircle2, Clock, Swords, Trophy } from 'lucide-react';

// Types for our bots
interface Bot {
  id: string;
  name: string;
  elo: number;
  description: string;
  lastUpdated: string;
  status: string;
}

// Challenge status type
type ChallengeStatus = 'idle' | 'submitting' | 'success' | 'error';

export function ChallengeContent() {
  const router = useRouter();
  const [myBots, setMyBots] = useState<Bot[]>([]);
  const [opponentBots, setOpponentBots] = useState<Bot[]>([]);
  const [selectedMyBot, setSelectedMyBot] = useState<string>('');
  const [selectedOpponentBot, setSelectedOpponentBot] = useState<string>('');
  const [challengeStatus, setChallengeStatus] = useState<ChallengeStatus>('idle');
  const [statusMessage, setStatusMessage] = useState<string>('');
  const [matchId, setMatchId] = useState<number | null>(null);
  const [estimatedTime, setEstimatedTime] = useState<string>('');
  const [isLoading, setIsLoading] = useState(true);

  // Get the selected bot objects
  const myBot = myBots.find((bot) => bot.id === selectedMyBot);
  const opponentBot = opponentBots.find((bot) => bot.id === selectedOpponentBot);

  // Fetch my bots
  useEffect(() => {
    async function fetchMyBots() {
      try {
        const response = await fetch('/api/my-bots');
        if (!response.ok) throw new Error('Failed to fetch my bots');
        const data = await response.json();
        setMyBots(data);
      } catch (error) {
        console.error('Error fetching my bots:', error);
      }
    }

    // Fetch opponent bots (in a real app, this would be a different endpoint)
    async function fetchOpponentBots() {
      try {
        // For demo purposes, we'll use a mock list of opponent bots
        // In a real app, this would be a separate API call
        const mockOpponents = [
          {
            id: 'opp1',
            name: 'GrandmasterAI',
            elo: 2100,
            description: 'Tournament winner with advanced positional play',
            lastUpdated: '2023-12-20T11:20:00Z',
            status: 'active',
          },
          {
            id: 'opp2',
            name: 'TacticalGenius',
            elo: 1950,
            description: 'Specializes in tactical combinations',
            lastUpdated: '2024-01-10T16:30:00Z',
            status: 'active',
          },
          {
            id: 'opp3',
            name: 'EndgameWizard',
            elo: 1880,
            description: 'Exceptional endgame knowledge',
            lastUpdated: '2023-11-15T08:45:00Z',
            status: 'active',
          },
          {
            id: 'opp4',
            name: 'OpeningMaster',
            elo: 1820,
            description: 'Extensive opening book knowledge',
            lastUpdated: '2023-12-05T14:10:00Z',
            status: 'active',
          },
          {
            id: 'opp5',
            name: 'DefensiveWall',
            elo: 1750,
            description: 'Extremely solid defensive play',
            lastUpdated: '2024-01-02T09:55:00Z',
            status: 'active',
          },
        ];
        setOpponentBots(mockOpponents);
      } catch (error) {
        console.error('Error fetching opponent bots:', error);
      } finally {
        setIsLoading(false);
      }
    }

    fetchMyBots();
    fetchOpponentBots();
  }, []);

  // Handle challenge submission
  const handleSubmitChallenge = async () => {
    if (!selectedMyBot || !selectedOpponentBot) {
      setChallengeStatus('error');
      setStatusMessage('Please select both your bot and an opponent bot');
      return;
    }

    setChallengeStatus('submitting');
    setStatusMessage('Submitting challenge...');

    try {
      const response = await fetch('/api/challenge', {
        method: 'POST',
        headers: {
          'Content-Type': 'application/json',
        },
        body: JSON.stringify({
          myBotId: selectedMyBot,
          opponentBotId: selectedOpponentBot,
        }),
      });

      const data = await response.json();

      if (!response.ok) {
        throw new Error(data.error || 'Failed to submit challenge');
      }

      setChallengeStatus('success');
      setStatusMessage(data.message);
      setMatchId(data.matchId);
      setEstimatedTime(data.estimatedCompletionTime);
    } catch (error) {
      console.error('Error submitting challenge:', error);
      setChallengeStatus('error');
      setStatusMessage(error instanceof Error ? error.message : 'An unknown error occurred');
    }
  };

  // View match results
  const handleViewMatches = () => {
    router.push('/matches');
  };

  return (
    <div className="space-y-6">
      <Card>
        <CardHeader>
          <CardTitle className="flex items-center gap-2">
            <Swords className="h-5 w-5" />
            Challenge a Bot
          </CardTitle>
          <CardDescription>
            Select one of your bots to challenge another bot. The match will be processed and results will be available on the matches page.
          </CardDescription>
        </CardHeader>
        <CardContent>
          {isLoading ? (
            <div className="flex justify-center py-8">
              <div className="animate-pulse text-center">
                <p>Loading bots...</p>
              </div>
            </div>
          ) : (
            <div className="space-y-8">
              {/* Bot selection section */}
              <div className="grid gap-6 md:grid-cols-2">
                {/* My bot selection */}
                <div className="space-y-4">
                  <div>
                    <h3 className="text-sm font-medium mb-2">Select Your Bot</h3>
                    <Select value={selectedMyBot} onValueChange={setSelectedMyBot}>
                      <SelectTrigger>
                        <SelectValue placeholder="Select one of your bots" />
                      </SelectTrigger>
                      <SelectContent>
                        {myBots.map((bot) => (
                          <SelectItem key={bot.id} value={bot.id}>
                            {bot.name} (ELO: {bot.elo})
                          </SelectItem>
                        ))}
                      </SelectContent>
                    </Select>
                  </div>

                  {myBot && (
                    <Card>
                      <CardHeader className="p-4">
                        <CardTitle className="text-base">{myBot.name}</CardTitle>
                        <div className="flex items-center gap-2">
                          <Badge variant="outline" className="flex items-center gap-1">
                            <Trophy className="h-3 w-3" />
                            ELO: {myBot.elo}
                          </Badge>
                        </div>
                      </CardHeader>
                      <CardContent className="p-4 pt-0">
                        <p className="text-sm text-muted-foreground">{myBot.description}</p>
                      </CardContent>
                    </Card>
                  )}
                </div>

                {/* Opponent bot selection */}
                <div className="space-y-4">
                  <div>
                    <h3 className="text-sm font-medium mb-2">Select Opponent Bot</h3>
                    <Select value={selectedOpponentBot} onValueChange={setSelectedOpponentBot}>
                      <SelectTrigger>
                        <SelectValue placeholder="Select an opponent bot" />
                      </SelectTrigger>
                      <SelectContent>
                        {opponentBots.map((bot) => (
                          <SelectItem key={bot.id} value={bot.id}>
                            {bot.name} (ELO: {bot.elo})
                          </SelectItem>
                        ))}
                      </SelectContent>
                    </Select>
                  </div>

                  {opponentBot && (
                    <Card>
                      <CardHeader className="p-4">
                        <CardTitle className="text-base">{opponentBot.name}</CardTitle>
                        <div className="flex items-center gap-2">
                          <Badge variant="outline" className="flex items-center gap-1">
                            <Trophy className="h-3 w-3" />
                            ELO: {opponentBot.elo}
                          </Badge>
                        </div>
                      </CardHeader>
                      <CardContent className="p-4 pt-0">
                        <p className="text-sm text-muted-foreground">{opponentBot.description}</p>
                      </CardContent>
                    </Card>
                  )}
                </div>
              </div>

              {/* Match preview */}
              {myBot && opponentBot && (
                <Card className="border-dashed">
                  <CardHeader>
                    <CardTitle className="text-base">Match Preview</CardTitle>
                  </CardHeader>
                  <CardContent>
                    <div className="flex flex-col md:flex-row items-center justify-center gap-4 py-2">
                      <div className="text-center">
                        <div className="font-medium">{myBot.name}</div>
                        <div className="text-sm text-muted-foreground">ELO: {myBot.elo}</div>
                      </div>
                      <div className="flex items-center gap-2">
                        <Swords className="h-5 w-5 text-muted-foreground" />
                        <ArrowRight className="h-4 w-4 text-muted-foreground" />
                      </div>
                      <div className="text-center">
                        <div className="font-medium">{opponentBot.name}</div>
                        <div className="text-sm text-muted-foreground">ELO: {opponentBot.elo}</div>
                      </div>
                    </div>
                    <div className="mt-4 text-sm text-center text-muted-foreground">
                      <p>ELO Difference: {Math.abs(myBot.elo - opponentBot.elo)}</p>
                      <p className="mt-1">
                        {myBot.elo > opponentBot.elo
                          ? 'Your bot has a higher ELO rating.'
                          : myBot.elo < opponentBot.elo
                            ? 'The opponent bot has a higher ELO rating.'
                            : 'Both bots have the same ELO rating.'}
                      </p>
                    </div>
                  </CardContent>
                </Card>
              )}

              {/* Status messages */}
              {challengeStatus === 'success' && (
                <Alert variant="default" className="bg-green-50 border-green-200">
                  <CheckCircle2 className="h-4 w-4 text-green-600" />
                  <AlertTitle>Challenge Submitted!</AlertTitle>
                  <AlertDescription>
                    <p>Your challenge has been submitted successfully. Match ID: {matchId}</p>
                    <div className="flex items-center gap-1 mt-2 text-sm">
                      <Clock className="h-3 w-3" />
                      <span>Estimated completion time: {estimatedTime}</span>
                    </div>
                    <Button variant="outline" size="sm" className="mt-4" onClick={handleViewMatches}>
                      View All Matches
                    </Button>
                  </AlertDescription>
                </Alert>
              )}

              {challengeStatus === 'error' && (
                <Alert variant="destructive">
                  <AlertCircle className="h-4 w-4" />
                  <AlertTitle>Error</AlertTitle>
                  <AlertDescription>{statusMessage}</AlertDescription>
                </Alert>
              )}
            </div>
          )}
        </CardContent>
        <CardFooter className="flex justify-end gap-2">
          <Button
            variant="outline"
            onClick={() => {
              setSelectedMyBot('');
              setSelectedOpponentBot('');
              setChallengeStatus('idle');
            }}
            disabled={challengeStatus === 'submitting' || challengeStatus === 'success'}
          >
            Reset
          </Button>
          <Button
            onClick={handleSubmitChallenge}
            disabled={!selectedMyBot || !selectedOpponentBot || challengeStatus === 'submitting' || challengeStatus === 'success'}
          >
            {challengeStatus === 'submitting' ? 'Submitting...' : 'Submit Challenge'}
          </Button>
        </CardFooter>
      </Card>
    </div>
  );
}
