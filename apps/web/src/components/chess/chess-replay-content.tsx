'use client';

import { useCallback, useEffect, useRef, useState } from 'react';
import { Chess } from 'chess.js';
import { Chessboard } from 'react-chessboard';
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/chess/ui/card';
import { Button } from '@/components/chess/ui/button';
import { Skeleton } from '@/components/chess/ui/skeleton';
import { Alert, AlertDescription, AlertTitle } from '@/components/chess/ui/alert';
import { Badge } from '@/components/chess/ui/badge';
import { Slider } from '@/components/chess/ui/slider';
import { AlertTriangle, Calendar, Clock, Pause, Play, SkipBack, SkipForward, StepBack, StepForward, Trophy, Users } from 'lucide-react';
import { formatDistanceToNow } from 'date-fns';
import { Api, CompetitionsApi } from '@game-guild/apiclient';
import { getSession } from 'next-auth/react';
import { GetSessionReturnType } from '@/configs/auth.config';
import ChessMatchResultDto = Api.ChessMatchResultDto;
import ApiErrorResponseDto = Api.ApiErrorResponseDto;

interface ChessReplayContentProps {
  matchId: string;
}

export default function ChessReplayContent({ matchId }: ChessReplayContentProps) {
  // Add debug log when component mounts
  console.log('[ChessReplayContent] Component rendering with matchId:', matchId);

  const [match, setMatch] = useState<ChessMatchResultDto | null>(null);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);

  // States for chess positions
  const [states, setStates] = useState<string[]>([]);
  const [currentStateId, setCurrentStateId] = useState(0);
  const [isPlaying, setIsPlaying] = useState(false);
  const [playbackSpeed, setPlaybackSpeed] = useState(1); // Moves per second
  const [algebraicMoves, setAlgebraicMoves] = useState<string[]>([]);

  // Ref for the board container and board width state
  const boardContainerRef = useRef<HTMLDivElement>(null);
  const [boardWidth, setBoardWidth] = useState(450);

  const api = new CompetitionsApi({
    basePath: process.env.NEXT_PUBLIC_API_URL,
  });

  // Calculate board width based on container size
  useEffect(() => {
    console.log('[ChessReplayContent] Setting up board width calculation');

    const updateBoardWidth = () => {
      if (boardContainerRef.current) {
        const containerWidth = boardContainerRef.current.clientWidth;
        setBoardWidth(Math.min(containerWidth, 450)); // Cap at 450px max
        console.log('[ChessReplayContent] Board width updated to:', Math.min(containerWidth, 450));
      }
    };

    // Initial calculation
    updateBoardWidth();

    // Set up resize observer to recalculate when container size changes
    const resizeObserver = new ResizeObserver(updateBoardWidth);
    if (boardContainerRef.current) {
      resizeObserver.observe(boardContainerRef.current);
    }

    // Clean up
    return () => {
      if (boardContainerRef.current) {
        resizeObserver.unobserve(boardContainerRef.current);
      }
      resizeObserver.disconnect();
    };
  }, []);

  // Fetch match data
  useEffect(() => {
    console.log('[ChessReplayContent] useEffect for fetching match data triggered');
    console.log('[ChessReplayContent] matchId in useEffect:', matchId);

    const fetchMatchData = async () => {
      try {
        console.log('[ChessReplayContent] Starting to fetch match data for matchId:', matchId);
        setLoading(true);
        const session = (await getSession()) as unknown as GetSessionReturnType;
        const response = await api.competitionControllerGetChessMatchResult(matchId, {
          headers: {
            Authorization: `Bearer ${session?.user?.accessToken}`,
          },
        });
        console.log('[ChessReplayContent] Fetch response status:', response.status);

        if (response.status >= 300) {
          const error = response.body as ApiErrorResponseDto;
          console.error('[ChessReplayContent] API error response:', error.error);
          throw new Error(`Failed to fetch match data: ${error.message}`);
        }

        const data = response.body as ChessMatchResultDto;
        console.log('[ChessReplayContent] Successfully fetched match data:', data);
        setMatch(data);

        // Generate chess states from UCI moves
        console.log('[ChessReplayContent] Generating chess states from UCI moves');
        const chess = new Chess();
        const list: string[] = [];
        const algebraic: string[] = [];

        // Initial position
        list.push(chess.fen());
        console.log('[ChessReplayContent] Initial FEN:', chess.fen());

        // Apply each UCI move and store the resulting position
        for (const uciMove of data.moves) {
          try {
            console.log('[ChessReplayContent] Processing UCI move:', uciMove);

            // Convert UCI move to chess.js move object
            const from = uciMove.substring(0, 2);
            const to = uciMove.substring(2, 4);
            const promotion = uciMove.length > 4 ? uciMove.substring(4, 5) : undefined;

            console.log('[ChessReplayContent] Converted to from/to:', { from, to, promotion });

            // Check if the move is valid before attempting to make it
            const possibleMoves = chess.moves({ verbose: true });
            const isValidMove = possibleMoves.some((move) => move.from === from && move.to === to && (!promotion || move.promotion === promotion));

            if (!isValidMove) {
              console.warn(`[ChessReplayContent] Skipping invalid move: ${uciMove} in position: ${chess.fen()}`);
              continue;
            }

            // Make the move
            const move = chess.move({ from, to, promotion });
            console.log('[ChessReplayContent] Move result:', move);

            // Store the algebraic notation for display
            if (move) {
              algebraic.push(move.san);
              console.log('[ChessReplayContent] Added algebraic notation:', move.san);
            }

            // Store the resulting position
            list.push(chess.fen());
            console.log('[ChessReplayContent] Added FEN:', chess.fen());
          } catch (moveError) {
            console.error('[ChessReplayContent] Error processing move:', uciMove, moveError);
            // Continue with the next move instead of breaking the entire replay
          }
        }

        console.log('[ChessReplayContent] Generated', list.length, 'states and', algebraic.length, 'algebraic moves');
        setStates(list);
        setAlgebraicMoves(algebraic);
        setCurrentStateId(0);
      } catch (err) {
        console.error('[ChessReplayContent] Error fetching match data:', err);
        setError(err instanceof Error ? err.message : 'Failed to fetch match data');
      } finally {
        setLoading(false);
        console.log('[ChessReplayContent] Finished fetching match data');
      }
    };

    if (matchId) {
      console.log('[ChessReplayContent] matchId is valid, calling fetchMatchData');
      fetchMatchData();
    } else {
      console.error('[ChessReplayContent] matchId is invalid, not fetching data');
      setError('Invalid match ID');
      setLoading(false);
    }
  }, [matchId]);

  // Handle auto-play
  useEffect(() => {
    let intervalId: NodeJS.Timeout | null = null;

    if (isPlaying && states.length > 0) {
      console.log('[ChessReplayContent] Starting auto-play interval');
      intervalId = setInterval(() => {
        setCurrentStateId((prevId) => {
          const nextId = prevId + 1;
          if (nextId >= states.length) {
            console.log('[ChessReplayContent] Auto-play reached end, stopping');
            setIsPlaying(false);
            return prevId;
          }
          console.log('[ChessReplayContent] Auto-play advancing to state:', nextId);
          return nextId;
        });
      }, 1000 / playbackSpeed);
    }

    return () => {
      if (intervalId) {
        console.log('[ChessReplayContent] Clearing auto-play interval');
        clearInterval(intervalId);
      }
    };
  }, [isPlaying, states.length, playbackSpeed]);

  // Navigation functions
  const goToStart = useCallback(() => {
    console.log('[ChessReplayContent] Going to start');
    setCurrentStateId(0);
    setIsPlaying(false);
  }, []);

  const goToEnd = useCallback(() => {
    if (states.length > 0) {
      console.log('[ChessReplayContent] Going to end:', states.length - 1);
      setCurrentStateId(states.length - 1);
      setIsPlaying(false);
    }
  }, [states.length]);

  const goToPrevMove = useCallback(() => {
    console.log('[ChessReplayContent] Going to previous move');
    setCurrentStateId((prev) => {
      const newState = Math.max(0, prev - 1);
      console.log('[ChessReplayContent] New state:', newState);
      return newState;
    });
    setIsPlaying(false);
  }, []);

  const goToNextMove = useCallback(() => {
    console.log('[ChessReplayContent] Going to next move');
    setCurrentStateId((prev) => {
      const newState = Math.min(states.length - 1, prev + 1);
      console.log('[ChessReplayContent] New state:', newState);
      return newState;
    });
  }, [states.length]);

  const togglePlayPause = useCallback(() => {
    if (states.length === 0) return;

    console.log('[ChessReplayContent] Toggling play/pause');

    // If we're at the end, go back to start when play is pressed
    if (currentStateId >= states.length - 1 && !isPlaying) {
      console.log('[ChessReplayContent] At end, going back to start');
      setCurrentStateId(0);
    }

    setIsPlaying((prev) => {
      const newState = !prev;
      console.log('[ChessReplayContent] New playing state:', newState);
      return newState;
    });
  }, [states.length, currentStateId, isPlaying]);

  const handleSliderChange = useCallback(
    (value: number[]) => {
      if (states.length === 0) return;
      console.log('[ChessReplayContent] Slider changed to:', value[0]);
      setCurrentStateId(value[0]);
      setIsPlaying(false);
    },
    [states.length],
  );

  // Format move notation
  const formatMoveNotation = useCallback((moves: string[]) => {
    return moves.map((move, index) => {
      const moveNumber = Math.floor(index / 2) + 1;
      const isWhiteMove = index % 2 === 0;

      if (isWhiteMove) {
        return `${moveNumber}. ${move}`;
      } else {
        return move;
      }
    });
  }, []);

  // Format CPU time
  const formatCpuTime = useCallback((ms: number) => {
    if (ms < 1000) return `${ms}ms`;
    return `${(ms / 1000).toFixed(2)}s`;
  }, []);

  if (loading) {
    console.log('[ChessReplayContent] Rendering loading state');
    return (
      <div className="space-y-6">
        <h1 className="text-3xl font-bold tracking-tight">Match Replay</h1>
        <Card>
          <CardHeader>
            <Skeleton className="h-8 w-64" />
            <Skeleton className="h-4 w-48" />
          </CardHeader>
          <CardContent>
            <div className="grid grid-cols-1 md:grid-cols-2 gap-6">
              <Skeleton className="aspect-square w-full max-w-[600px]" />
              <div className="space-y-4">
                <Skeleton className="h-8 w-full" />
                <Skeleton className="h-32 w-full" />
                <Skeleton className="h-8 w-full" />
              </div>
            </div>
          </CardContent>
        </Card>
      </div>
    );
  }

  if (error) {
    console.log('[ChessReplayContent] Rendering error state:', error);
    return (
      <div className="space-y-6">
        <h1 className="text-3xl font-bold tracking-tight">Match Replay</h1>
        <Alert variant="destructive">
          <AlertTriangle className="h-4 w-4" />
          <AlertTitle>Error</AlertTitle>
          <AlertDescription>{error}</AlertDescription>
        </Alert>
      </div>
    );
  }

  if (!match || states.length <= 1) {
    console.log('[ChessReplayContent] Rendering no match data state');
    return (
      <div className="space-y-6">
        <h1 className="text-3xl font-bold tracking-tight">Match Replay</h1>
        <Alert variant="destructive">
          <AlertTriangle className="h-4 w-4" />
          <AlertTitle>Error</AlertTitle>
          <AlertDescription>Match data not found or could not be processed</AlertDescription>
        </Alert>
      </div>
    );
  }

  console.log('[ChessReplayContent] Rendering full match replay UI');
  const formattedMoves = formatMoveNotation(algebraicMoves);
  const currentMoveNumber = currentStateId > 0 ? Math.ceil(currentStateId / 2) : 0;

  return (
    <div className="space-y-6">
      <h1 className="text-3xl font-bold tracking-tight">Match Replay</h1>

      <Card>
        <CardHeader>
          <div className="flex flex-col md:flex-row md:items-center justify-between gap-4">
            <div>
              <CardTitle>
                {match.players[0]} vs {match.players[1]}
              </CardTitle>
              <CardDescription>Match ID: {match.id}</CardDescription>
            </div>
            <div className="flex flex-wrap gap-2">
              <Badge variant="outline" className="flex items-center gap-1">
                <Calendar className="h-3 w-3" />
                {formatDistanceToNow(new Date(match.createdAt), { addSuffix: true })}
              </Badge>
              {match.draw ? (
                <Badge className="bg-yellow-500">Draw</Badge>
              ) : (
                <Badge className="bg-green-500 flex items-center gap-1">
                  <Trophy className="h-3 w-3" />
                  Winner: {match.winner}
                </Badge>
              )}
              <Badge variant="secondary" className="flex items-center gap-1">
                <Users className="h-3 w-3" />
                {match.result} ({match.reason})
              </Badge>
            </div>
          </div>
        </CardHeader>
        <CardContent>
          <div className="grid grid-cols-1 lg:grid-cols-2 gap-6">
            {/* Chess board */}
            <div className="flex flex-col items-center">
              <div ref={boardContainerRef} className="w-full max-w-[600px] overflow-hidden">
                <Chessboard
                  position={states[currentStateId]}
                  boardWidth={boardWidth}
                  arePiecesDraggable={false}
                  customDarkSquareStyle={{ backgroundColor: '#B58863' }}
                  customLightSquareStyle={{ backgroundColor: '#F0D9B5' }}
                />
              </div>

              {/* Playback controls */}
              <div className="mt-4 w-full max-w-[600px]">
                <div className="flex items-center justify-between mb-2">
                  <span className="text-sm">
                    Move: {currentStateId === 0 ? 'Start' : `${currentMoveNumber}${currentStateId % 2 === 0 ? ' (Black)' : ' (White)'}`}
                  </span>
                  <span className="text-sm">{algebraicMoves.length} moves total</span>
                </div>

                <Slider value={[currentStateId]} min={0} max={states.length - 1} step={1} onValueChange={handleSliderChange} className="mb-4" />

                <div className="flex items-center justify-center gap-2">
                  <Button variant="outline" size="icon" onClick={goToStart} title="Go to start">
                    <SkipBack className="h-4 w-4" />
                  </Button>
                  <Button variant="outline" size="icon" onClick={goToPrevMove} title="Previous move">
                    <StepBack className="h-4 w-4" />
                  </Button>
                  <Button onClick={togglePlayPause} title={isPlaying ? 'Pause' : 'Play'}>
                    {isPlaying ? <Pause className="h-4 w-4" /> : <Play className="h-4 w-4" />}
                  </Button>
                  <Button variant="outline" size="icon" onClick={goToNextMove} title="Next move">
                    <StepForward className="h-4 w-4" />
                  </Button>
                  <Button variant="outline" size="icon" onClick={goToEnd} title="Go to end">
                    <SkipForward className="h-4 w-4" />
                  </Button>
                </div>

                <div className="mt-4 flex items-center justify-center gap-2">
                  <span className="text-sm">Speed:</span>
                  <Button variant={playbackSpeed === 0.5 ? 'default' : 'outline'} size="sm" onClick={() => setPlaybackSpeed(0.5)}>
                    0.5x
                  </Button>
                  <Button variant={playbackSpeed === 1 ? 'default' : 'outline'} size="sm" onClick={() => setPlaybackSpeed(1)}>
                    1x
                  </Button>
                  <Button variant={playbackSpeed === 2 ? 'default' : 'outline'} size="sm" onClick={() => setPlaybackSpeed(2)}>
                    2x
                  </Button>
                  <Button variant={playbackSpeed === 4 ? 'default' : 'outline'} size="sm" onClick={() => setPlaybackSpeed(4)}>
                    4x
                  </Button>
                </div>
              </div>
            </div>

            {/* Match details and move list */}
            <div className="space-y-4">
              <div className="grid grid-cols-2 gap-4">
                <Card>
                  <CardHeader className="p-4">
                    <CardTitle className="text-lg flex items-center gap-2">
                      <div className="w-4 h-4 bg-white border border-gray-300 rounded-full"></div>
                      {match.players[0]}
                    </CardTitle>
                  </CardHeader>
                  <CardContent className="p-4 pt-0">
                    <div className="flex flex-col gap-1">
                      <div className="flex justify-between">
                        <span className="text-sm text-muted-foreground">ELO:</span>
                        <span className="font-medium">{match.elo[0]}</span>
                      </div>
                      <div className="flex justify-between">
                        <span className="text-sm text-muted-foreground">ELO Change:</span>
                        <span className={`font-medium ${match.eloChange[0] >= 0 ? 'text-green-500' : 'text-red-500'}`}>
                          {match.eloChange[0] >= 0 ? '+' : ''}
                          {match.eloChange[0]}
                        </span>
                      </div>
                      <div className="flex justify-between">
                        <span className="text-sm text-muted-foreground">CPU Time:</span>
                        <span className="font-medium flex items-center gap-1">
                          <Clock className="h-3 w-3" />
                          {formatCpuTime(match.cpuTime[0])}
                        </span>
                      </div>
                    </div>
                  </CardContent>
                </Card>

                <Card>
                  <CardHeader className="p-4">
                    <CardTitle className="text-lg flex items-center gap-2">
                      <div className="w-4 h-4 bg-black border border-gray-300 rounded-full"></div>
                      {match.players[1]}
                    </CardTitle>
                  </CardHeader>
                  <CardContent className="p-4 pt-0">
                    <div className="flex flex-col gap-1">
                      <div className="flex justify-between">
                        <span className="text-sm text-muted-foreground">ELO:</span>
                        <span className="font-medium">{match.elo[1]}</span>
                      </div>
                      <div className="flex justify-between">
                        <span className="text-sm text-muted-foreground">ELO Change:</span>
                        <span className={`font-medium ${match.eloChange[1] >= 0 ? 'text-green-500' : 'text-red-500'}`}>
                          {match.eloChange[1] >= 0 ? '+' : ''}
                          {match.eloChange[1]}
                        </span>
                      </div>
                      <div className="flex justify-between">
                        <span className="text-sm text-muted-foreground">CPU Time:</span>
                        <span className="font-medium flex items-center gap-1">
                          <Clock className="h-3 w-3" />
                          {formatCpuTime(match.cpuTime[1])}
                        </span>
                      </div>
                    </div>
                  </CardContent>
                </Card>
              </div>

              {/* Move list */}
              <Card>
                <CardHeader className="p-4">
                  <CardTitle className="text-lg">Move History</CardTitle>
                </CardHeader>
                <CardContent className="p-4 pt-0">
                  <div className="h-[300px] overflow-y-auto border rounded-md p-2">
                    <div className="grid grid-cols-2 gap-2">
                      {formattedMoves.map((move, index) => {
                        const isCurrentMove = index === currentStateId - 1;
                        return (
                          <div
                            key={index}
                            className={`px-2 py-1 rounded ${isCurrentMove ? 'bg-primary text-primary-foreground' : index % 2 === 0 ? 'bg-muted' : ''}`}
                          >
                            {move}
                          </div>
                        );
                      })}
                    </div>
                  </div>
                </CardContent>
              </Card>
            </div>
          </div>
        </CardContent>
      </Card>
    </div>
  );
}
