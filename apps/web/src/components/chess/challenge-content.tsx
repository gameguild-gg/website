'use client';

import { useEffect, useState } from 'react';
import { useRouter } from 'next/navigation';
import { Card, CardContent, CardDescription, CardFooter, CardHeader, CardTitle } from '@/components/ui/card';
import { Button } from '@/components/ui/button';
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from '@/components/ui/select';
import { Alert, AlertDescription, AlertTitle } from '@/components/ui/alert';
import { Badge } from '@/components/ui/badge';
import { AlertCircle, ArrowRight, CheckCircle2, Clock, Swords, Trophy } from 'lucide-react';
import { Api, CompetitionsApi, UsersApi } from '@game-guild/apiclient';
import { getSession } from 'next-auth/react';
import { GetSessionReturnType } from '@/configs/auth.config';
import ChessAgentResponseEntryDto = Api.ChessAgentResponseEntryDto;

// Challenge status type
type ChallengeStatus = 'idle' | 'submitting' | 'success' | 'error';

// Add color type
type BotColor = 'white' | 'black';

export function ChallengeContent() {
  const router = useRouter();
  const [myBot, setMyBot] = useState<ChessAgentResponseEntryDto | null>(null);
  // UI state
  const [availableBots, setAvailableBots] = useState<ChessAgentResponseEntryDto[]>([]);
  const [selectedOpponentBot, setSelectedOpponentBot] = useState<string>('');
  const [selectedColor, setSelectedColor] = useState<BotColor>('white');
  const [challengeStatus, setChallengeStatus] = useState<ChallengeStatus>('idle');
  const [statusMessage, setStatusMessage] = useState<string>('');
  const [matchId, setMatchId] = useState<number | null>(null);
  const [estimatedTime, setEstimatedTime] = useState<number>(0);
  const [isLoading, setIsLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);

  const api = new CompetitionsApi({
    basePath: process.env.NEXT_PUBLIC_API_URL,
  });

  const usersApi = new UsersApi({
    basePath: process.env.NEXT_PUBLIC_API_URL,
  });

  // Get the selected opponent bot object
  const opponentBot = availableBots.find((bot) => bot.id === selectedOpponentBot);

  // Fetch my bot and opponent bots
  useEffect(() => {
    const fetchBots = async () => {
      try {
        setIsLoading(true);
        const session = (await getSession()) as unknown as GetSessionReturnType;
        console.log('before request');
        const response = await api.competitionControllerListChessAgents({
          headers: {
            Authorization: `Bearer ${session?.user?.accessToken}`,
          },
        });

        if (response.status === 401) {
          setError('You are not authorized to view this page.');
          return;
        }

        if (response.status === 500) {
          setError('Internal server error. Please report this issue to the community.');
          setError(JSON.stringify(response.body));
          return;
        }

        const data = response.body as ChessAgentResponseEntryDto[];

        setAvailableBots(data);

        // get the bot in the data with the same username as the user and set as myBot
        const username = JSON.parse(atob(session?.user?.accessToken.split('.')[1])).username as string;
        const localMyBot = data.find((bot) => bot.username === username) || null;
        setMyBot(localMyBot);
        if (!localMyBot) {
          setError('You do not have a bot yet. Please submit a bot first.');
          return;
        }

        setError(null);
      } catch (err) {
        console.error('Error fetching bots:', err);
        setError('Failed to load available bots. Please try again later.');
      } finally {
        setIsLoading(false);
      }
    };

    fetchBots();
  }, []);

  // Handle challenge submission
  const handleSubmitChallenge = async () => {
    if (!myBot || !selectedOpponentBot) {
      setChallengeStatus('error');
      setStatusMessage('Please select an opponent bot');
      return;
    }

    setChallengeStatus('submitting');
    setStatusMessage('Submitting challenge...');

    try {
      const session = (await getSession()) as unknown as GetSessionReturnType;
      const response = await api.competitionControllerRunChessMatch(
        {
          player1username: selectedColor == 'white' ? myBot.username : opponentBot.username,
          player2username: selectedColor == 'black' ? myBot.username : opponentBot.username,
        },
        {
          headers: {
            Authorization: `Bearer ${session?.user?.accessToken}`,
          },
        },
      );

      const data = response.body as Api.ChessMatchResultDto;

      setChallengeStatus('success');
      // setStatusMessage(data);
      // setMatchId(data.id);
      // setEstimatedTime(data.cpuTime[0] + data.cpuTime[1]);
    } catch (error) {
      console.error('Error submitting challenge:', error);
      setChallengeStatus('error');
      setStatusMessage(error instanceof Error ? error.message : 'An unknown error occurred');
    }
  };

  // View match results
  const handleViewMatches = () => {
    router.push('/chess/matches');
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
            Challenge your bot against another bot. The match will be processed and results will be available on the matches page.
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
              {/* Bot display section */}
              <div className="grid gap-6 md:grid-cols-2">
                {/* My bot display */}
                <div className="space-y-4">
                  <div>
                    <h3 className="text-sm font-medium mb-2">Your Bot</h3>
                  </div>

                  {myBot ? (
                    <Card>
                      <CardHeader className="p-4">
                        <CardTitle className="text-base">{myBot.username}</CardTitle>
                        <div className="flex items-center gap-2">
                          <Badge variant="outline" className="flex items-center gap-1">
                            <Trophy className="h-3 w-3" />
                            ELO: {myBot.elo}
                          </Badge>
                        </div>
                        <div className="mt-2">
                          <label className="text-sm font-medium">Play as:</label>
                          <div className="flex gap-2 mt-1">
                            <Button variant={selectedColor === 'white' ? 'default' : 'outline'} size="sm" onClick={() => setSelectedColor('white')}>
                              White
                            </Button>
                            <Button variant={selectedColor === 'black' ? 'default' : 'outline'} size="sm" onClick={() => setSelectedColor('black')}>
                              Black
                            </Button>
                          </div>
                        </div>
                      </CardHeader>
                    </Card>
                  ) : (
                    <Alert>
                      <AlertCircle className="h-4 w-4" />
                      <AlertTitle>No Bot Found</AlertTitle>
                      <AlertDescription>
                        You don't have a bot yet. Please submit a bot first.
                        <Button variant="link" className="p-0 h-auto font-normal" onClick={() => router.push('/submit')}>
                          Submit a bot
                        </Button>
                      </AlertDescription>
                    </Alert>
                  )}
                </div>

                {/* Opponent bot selection */}
                <div className="space-y-4">
                  <div>
                    <h3 className="text-sm font-medium mb-2">Select Opponent Bot</h3>
                    <Select value={selectedOpponentBot} onValueChange={setSelectedOpponentBot} disabled={!myBot}>
                      <SelectTrigger>
                        <SelectValue placeholder="Select an opponent bot" />
                      </SelectTrigger>
                      <SelectContent>
                        {availableBots.map((bot) => (
                          <SelectItem key={bot.id} value={bot.id}>
                            {bot.username} (ELO: {bot.elo})
                          </SelectItem>
                        ))}
                      </SelectContent>
                    </Select>
                  </div>

                  {opponentBot && (
                    <Card>
                      <CardHeader className="p-4">
                        <CardTitle className="text-base">{opponentBot.username}</CardTitle>
                        <div className="flex items-center gap-2">
                          <Badge variant="outline" className="flex items-center gap-1">
                            <Trophy className="h-3 w-3" />
                            ELO: {opponentBot.elo}
                          </Badge>
                        </div>
                      </CardHeader>
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
                        <div className="font-medium">{selectedColor === 'white' ? myBot.username : opponentBot.username}</div>
                        <div className="text-sm text-muted-foreground">ELO: {selectedColor === 'white' ? myBot.elo : opponentBot.elo}</div>
                        <div className="text-sm font-medium text-muted-foreground">(White)</div>
                      </div>
                      <div className="flex items-center gap-2">
                        <Swords className="h-5 w-5 text-muted-foreground" />
                        <ArrowRight className="h-4 w-4 text-muted-foreground" />
                      </div>
                      <div className="text-center">
                        <div className="font-medium">{selectedColor === 'white' ? opponentBot.username : myBot.username}</div>
                        <div className="text-sm text-muted-foreground">ELO: {selectedColor === 'white' ? opponentBot.elo : myBot.elo}</div>
                        <div className="text-sm font-medium text-muted-foreground">(Black)</div>
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
              setSelectedOpponentBot('');
              setSelectedColor('white');
              setChallengeStatus('idle');
            }}
            disabled={challengeStatus === 'submitting' || challengeStatus === 'success' || !myBot}
          >
            Reset
          </Button>
          <Button
            onClick={handleSubmitChallenge}
            disabled={!myBot || !selectedOpponentBot || challengeStatus === 'submitting' || challengeStatus === 'success'}
          >
            {challengeStatus === 'submitting' ? 'Submitting...' : 'Submit Challenge'}
          </Button>
        </CardFooter>
      </Card>
    </div>
  );
}
