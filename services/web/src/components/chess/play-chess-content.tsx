'use client';

import { useCallback, useEffect, useRef, useState } from 'react';
import { Chess } from 'chess.js';
import { Chessboard } from 'react-chessboard';
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/chess/ui/card';
import { Button } from '@/components/chess/ui/button';
import { Skeleton } from '@/components/chess/ui/skeleton';
import { Alert, AlertDescription, AlertTitle } from '@/components/chess/ui/alert';
import { Badge } from '@/components/chess/ui/badge';
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from '@/components/chess/ui/select';
import { Switch } from '@/components/chess/ui/switch';
import { Label } from '@/components/chess/ui/label';
import { AlertTriangle, Info, Loader2, RotateCcw, Trophy } from 'lucide-react';

interface ChessBotDto {
  id: string;
  name: string;
  elo: number;
  author: string;
  description: string;
  createdAt: string;
}

type PlayerType = 'human' | 'bot';

interface PlayerConfig {
  type: PlayerType;
  botId: string | null;
}

export default function PlayChessContent() {
  // Board state
  const [game, setGame] = useState<Chess>(new Chess());
  const [fen, setFen] = useState<string>(new Chess().fen());
  const [isGameOver, setIsGameOver] = useState<boolean>(false);
  const [gameResult, setGameResult] = useState<any>(null);
  const [lastMove, setLastMove] = useState<{ from: string; to: string } | null>(null);

  // Player configuration
  const [whitePlayer, setWhitePlayer] = useState<PlayerConfig>({ type: 'human', botId: null });
  const [blackPlayer, setBlackPlayer] = useState<PlayerConfig>({ type: 'bot', botId: 'bot_1' });

  // UI state
  const [availableBots, setAvailableBots] = useState<ChessBotDto[]>([]);
  const [isLoading, setIsLoading] = useState<boolean>(true);
  const [isBotThinking, setIsBotThinking] = useState<boolean>(false);
  const [error, setError] = useState<string | null>(null);
  const [autoPlay, setAutoPlay] = useState<boolean>(false);
  const [gameStarted, setGameStarted] = useState<boolean>(false);

  // Ref for the board container and board width state
  const boardContainerRef = useRef<HTMLDivElement>(null);
  const [boardWidth, setBoardWidth] = useState(450);

  // Calculate board width based on container size
  useEffect(() => {
    const updateBoardWidth = () => {
      if (boardContainerRef.current) {
        const containerWidth = boardContainerRef.current.clientWidth;
        setBoardWidth(Math.min(containerWidth, 450));
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

  // Fetch available bots
  useEffect(() => {
    const fetchBots = async () => {
      try {
        setIsLoading(true);
        const response = await fetch('/api/bots');

        if (!response.ok) {
          throw new Error('Failed to fetch bots');
        }

        const data = await response.json();
        setAvailableBots(data);

        // Set default bots
        if (data.length > 0) {
          setBlackPlayer({ type: 'bot', botId: data[0].id });
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

  // Handle bot moves
  const makeBotMove = useCallback(async (currentFen: string, botId: string) => {
    if (!botId) return;

    setIsBotThinking(true);

    try {
      const response = await fetch('/api/play-move', {
        method: 'POST',
        headers: {
          'Content-Type': 'application/json',
        },
        body: JSON.stringify({
          fen: currentFen,
          botId,
        }),
      });

      if (!response.ok) {
        throw new Error('Failed to get bot move');
      }

      const data = await response.json();

      if (data.success) {
        // Update the game state
        const newGame = new Chess(data.fen);
        setGame(newGame);
        setFen(data.fen);
        setLastMove(data.lastMove);

        // Check if the game is over
        if (data.isGameOver) {
          setIsGameOver(true);
          setGameResult(data.result);
          setGameStarted(false);
        }

        return data;
      } else {
        throw new Error(data.message || 'Failed to get bot move');
      }
    } catch (err) {
      console.error('Error getting bot move:', err);
      setError('Failed to get bot move. Please try again.');
      return null;
    } finally {
      setIsBotThinking(false);
    }
  }, []);

  // Auto-play logic for bot vs bot games
  useEffect(() => {
    if (!gameStarted || isGameOver || !autoPlay) return;

    const currentTurn = game.turn() === 'w' ? 'white' : 'black';
    const currentPlayer = currentTurn === 'white' ? whitePlayer : blackPlayer;

    if (currentPlayer.type === 'bot' && currentPlayer.botId) {
      const timeoutId = setTimeout(() => {
        makeBotMove(game.fen(), currentPlayer.botId!);
      }, 1000);

      return () => clearTimeout(timeoutId);
    }
  }, [game, whitePlayer, blackPlayer, autoPlay, gameStarted, isGameOver, makeBotMove]);

  // Handle human moves
  const onDrop = (sourceSquare: string, targetSquare: string) => {
    if (isGameOver || isBotThinking) return false;

    const currentTurn = game.turn() === 'w' ? 'white' : 'black';
    const currentPlayer = currentTurn === 'white' ? whitePlayer : blackPlayer;

    // Only allow human moves if it's the human's turn
    if (currentPlayer.type !== 'human') return false;

    try {
      // Make the move
      const move = game.move({
        from: sourceSquare,
        to: targetSquare,
        promotion: 'q', // Always promote to queen for simplicity
      });

      // If the move is invalid, return false
      if (!move) return false;

      // Update the game state
      setFen(game.fen());
      setLastMove({ from: sourceSquare, to: targetSquare });

      // Check if the game is over
      if (game.isGameOver()) {
        setIsGameOver(true);
        setGameResult(getGameResult(game));
        setGameStarted(false);
        return true;
      }

      // If the next player is a bot and auto-play is enabled, make the bot move
      const nextTurn = game.turn() === 'w' ? 'white' : 'black';
      const nextPlayer = nextTurn === 'white' ? whitePlayer : blackPlayer;

      if (nextPlayer.type === 'bot' && nextPlayer.botId && autoPlay) {
        setTimeout(() => {
          makeBotMove(game.fen(), nextPlayer.botId!);
        }, 500);
      }

      return true;
    } catch (error) {
      console.error('Error making move:', error);
      return false;
    }
  };

  // Start a new game
  const startNewGame = () => {
    const newGame = new Chess();
    setGame(newGame);
    setFen(newGame.fen());
    setIsGameOver(false);
    setGameResult(null);
    setLastMove(null);
    setGameStarted(true);

    // If white player is a bot and auto-play is enabled, make the first move
    if (whitePlayer.type === 'bot' && whitePlayer.botId && autoPlay) {
      setTimeout(() => {
        makeBotMove(newGame.fen(), whitePlayer.botId!);
      }, 500);
    }
  };

  // Reset the game
  const resetGame = () => {
    const newGame = new Chess();
    setGame(newGame);
    setFen(newGame.fen());
    setIsGameOver(false);
    setGameResult(null);
    setLastMove(null);
    setGameStarted(false);
  };

  // Make a single bot move (for manual play)
  const makeNextBotMove = () => {
    if (isGameOver || isBotThinking) return;

    const currentTurn = game.turn() === 'w' ? 'white' : 'black';
    const currentPlayer = currentTurn === 'white' ? whitePlayer : blackPlayer;

    if (currentPlayer.type === 'bot' && currentPlayer.botId) {
      makeBotMove(game.fen(), currentPlayer.botId);
    }
  };

  // Helper function to determine game result
  function getGameResult(chess: Chess) {
    if (chess.isCheckmate()) return { type: 'checkmate', winner: chess.turn() === 'w' ? 'black' : 'white' };
    if (chess.isDraw()) {
      if (chess.isStalemate()) return { type: 'draw', reason: 'stalemate' };
      if (chess.isThreefoldRepetition()) return { type: 'draw', reason: 'threefold repetition' };
      if (chess.isInsufficientMaterial()) return { type: 'draw', reason: 'insufficient material' };
      return { type: 'draw', reason: '50-move rule' };
    }
    return { type: 'unknown' };
  }

  // Get the current bot name
  const getBotName = (botId: string | null) => {
    if (!botId) return 'None';
    const bot = availableBots.find((b) => b.id === botId);
    return bot ? bot.name : 'Unknown Bot';
  };

  // Format the game result for display
  const formatGameResult = () => {
    if (!gameResult) return null;

    if (gameResult.type === 'checkmate') {
      return `Checkmate! ${gameResult.winner === 'white' ? 'White' : 'Black'} wins.`;
    } else if (gameResult.type === 'draw') {
      return `Draw by ${gameResult.reason}.`;
    }

    return 'Game over.';
  };

  if (isLoading) {
    return (
      <div className="space-y-6">
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

  return (
    <div className="space-y-6">
      {error && (
        <Alert variant="destructive">
          <AlertTriangle className="h-4 w-4" />
          <AlertTitle>Error</AlertTitle>
          <AlertDescription>{error}</AlertDescription>
        </Alert>
      )}

      <Card>
        <CardHeader>
          <CardTitle>Chess Game</CardTitle>
          <CardDescription>Play against bots or watch bots play against each other</CardDescription>
        </CardHeader>
        <CardContent>
          <div className="grid grid-cols-1 lg:grid-cols-2 gap-6">
            {/* Chess board */}
            <div className="flex flex-col items-center">
              <div ref={boardContainerRef} className="w-full max-w-[600px] overflow-hidden">
                <Chessboard
                  position={fen}
                  boardWidth={boardWidth}
                  arePiecesDraggable={!isGameOver && !isBotThinking}
                  onPieceDrop={onDrop}
                  customDarkSquareStyle={{ backgroundColor: '#B58863' }}
                  customLightSquareStyle={{ backgroundColor: '#F0D9B5' }}
                  customSquareStyles={
                    lastMove
                      ? {
                          [lastMove.from]: { backgroundColor: 'rgba(255, 255, 0, 0.4)' },
                          [lastMove.to]: { backgroundColor: 'rgba(255, 255, 0, 0.4)' },
                        }
                      : {}
                  }
                />
              </div>

              {/* Game controls */}
              <div className="mt-4 w-full max-w-[600px]">
                <div className="flex flex-col sm:flex-row items-center justify-between gap-4 mb-4">
                  <div className="flex items-center gap-2">
                    <Button onClick={startNewGame} disabled={isBotThinking || (gameStarted && !isGameOver)}>
                      {gameStarted && !isGameOver ? 'Game in Progress' : 'Start Game'}
                    </Button>
                    <Button variant="outline" onClick={resetGame} disabled={isBotThinking}>
                      <RotateCcw className="h-4 w-4 mr-2" />
                      Reset
                    </Button>
                  </div>

                  <div className="flex items-center gap-2">
                    <Button
                      onClick={makeNextBotMove}
                      disabled={isGameOver || isBotThinking || !gameStarted || (game.turn() === 'w' ? whitePlayer.type !== 'bot' : blackPlayer.type !== 'bot')}
                    >
                      {isBotThinking ? (
                        <>
                          <Loader2 className="h-4 w-4 mr-2 animate-spin" />
                          Thinking...
                        </>
                      ) : (
                        'Make Bot Move'
                      )}
                    </Button>
                  </div>
                </div>

                {isGameOver && gameResult && (
                  <Alert className="mb-4">
                    <Trophy className="h-4 w-4" />
                    <AlertTitle>Game Over</AlertTitle>
                    <AlertDescription>{formatGameResult()}</AlertDescription>
                  </Alert>
                )}

                {!gameStarted && !isGameOver && (
                  <Alert className="mb-4">
                    <Info className="h-4 w-4" />
                    <AlertTitle>Ready to Play</AlertTitle>
                    <AlertDescription>Configure the players and click "Start Game" to begin.</AlertDescription>
                  </Alert>
                )}
              </div>
            </div>

            {/* Game settings */}
            <div className="space-y-4">
              <Card>
                <CardHeader className="p-4">
                  <CardTitle className="text-lg">Game Settings</CardTitle>
                </CardHeader>
                <CardContent className="p-4 pt-0 space-y-6">
                  {/* White player settings */}
                  <div className="space-y-2">
                    <div className="flex items-center justify-between">
                      <Label htmlFor="white-player" className="text-base font-medium">
                        White Player
                      </Label>
                      <div className="flex items-center space-x-2">
                        <Label htmlFor="white-human" className="text-sm">
                          Human
                        </Label>
                        <Switch
                          id="white-human"
                          checked={whitePlayer.type === 'human'}
                          onCheckedChange={(checked) => {
                            setWhitePlayer({
                              type: checked ? 'human' : 'bot',
                              botId: checked ? null : availableBots[0]?.id || null,
                            });
                          }}
                          disabled={gameStarted && !isGameOver}
                        />
                      </div>
                    </div>

                    {whitePlayer.type === 'bot' && (
                      <Select
                        value={whitePlayer.botId || ''}
                        onValueChange={(value) => {
                          setWhitePlayer({
                            ...whitePlayer,
                            botId: value,
                          });
                        }}
                        disabled={gameStarted && !isGameOver}
                      >
                        <SelectTrigger>
                          <SelectValue placeholder="Select a bot" />
                        </SelectTrigger>
                        <SelectContent>
                          {availableBots.map((bot) => (
                            <SelectItem key={bot.id} value={bot.id}>
                              {bot.name} (ELO: {bot.elo})
                            </SelectItem>
                          ))}
                        </SelectContent>
                      </Select>
                    )}

                    {whitePlayer.type === 'bot' && whitePlayer.botId && (
                      <div className="text-sm text-muted-foreground">{availableBots.find((b) => b.id === whitePlayer.botId)?.description}</div>
                    )}
                  </div>

                  {/* Black player settings */}
                  <div className="space-y-2">
                    <div className="flex items-center justify-between">
                      <Label htmlFor="black-player" className="text-base font-medium">
                        Black Player
                      </Label>
                      <div className="flex items-center space-x-2">
                        <Label htmlFor="black-human" className="text-sm">
                          Human
                        </Label>
                        <Switch
                          id="black-human"
                          checked={blackPlayer.type === 'human'}
                          onCheckedChange={(checked) => {
                            setBlackPlayer({
                              type: checked ? 'human' : 'bot',
                              botId: checked ? null : availableBots[0]?.id || null,
                            });
                          }}
                          disabled={gameStarted && !isGameOver}
                        />
                      </div>
                    </div>

                    {blackPlayer.type === 'bot' && (
                      <Select
                        value={blackPlayer.botId || ''}
                        onValueChange={(value) => {
                          setBlackPlayer({
                            ...blackPlayer,
                            botId: value,
                          });
                        }}
                        disabled={gameStarted && !isGameOver}
                      >
                        <SelectTrigger>
                          <SelectValue placeholder="Select a bot" />
                        </SelectTrigger>
                        <SelectContent>
                          {availableBots.map((bot) => (
                            <SelectItem key={bot.id} value={bot.id}>
                              {bot.name} (ELO: {bot.elo})
                            </SelectItem>
                          ))}
                        </SelectContent>
                      </Select>
                    )}

                    {blackPlayer.type === 'bot' && blackPlayer.botId && (
                      <div className="text-sm text-muted-foreground">{availableBots.find((b) => b.id === blackPlayer.botId)?.description}</div>
                    )}
                  </div>

                  {/* Auto-play setting */}
                  <div className="flex items-center space-x-2 pt-2">
                    <Switch id="auto-play" checked={autoPlay} onCheckedChange={setAutoPlay} disabled={gameStarted && !isGameOver} />
                    <Label htmlFor="auto-play">Auto-play bot moves</Label>
                  </div>
                </CardContent>
              </Card>

              {/* Current game info */}
              <Card>
                <CardHeader className="p-4">
                  <CardTitle className="text-lg">Current Game</CardTitle>
                </CardHeader>
                <CardContent className="p-4 pt-0">
                  <div className="space-y-2">
                    <div className="flex justify-between">
                      <span className="text-sm font-medium">White:</span>
                      <span>{whitePlayer.type === 'human' ? 'Human Player' : getBotName(whitePlayer.botId)}</span>
                    </div>
                    <div className="flex justify-between">
                      <span className="text-sm font-medium">Black:</span>
                      <span>{blackPlayer.type === 'human' ? 'Human Player' : getBotName(blackPlayer.botId)}</span>
                    </div>
                    <div className="flex justify-between">
                      <span className="text-sm font-medium">Current Turn:</span>
                      <Badge>{game.turn() === 'w' ? 'White' : 'Black'}</Badge>
                    </div>
                    <div className="flex justify-between">
                      <span className="text-sm font-medium">Status:</span>
                      <span>{isGameOver ? 'Game Over' : gameStarted ? 'In Progress' : 'Not Started'}</span>
                    </div>
                    <div className="flex justify-between">
                      <span className="text-sm font-medium">Move Number:</span>
                      <span>{Math.floor(game.moveNumber())}</span>
                    </div>
                  </div>
                </CardContent>
              </Card>

              {/* Move history */}
              <Card>
                <CardHeader className="p-4">
                  <CardTitle className="text-lg">Move History</CardTitle>
                </CardHeader>
                <CardContent className="p-4 pt-0">
                  <div className="h-[200px] overflow-y-auto border rounded-md p-2">
                    {game.history().length === 0 ? (
                      <div className="text-center text-muted-foreground py-4">No moves yet</div>
                    ) : (
                      <div className="grid grid-cols-2 gap-2">
                        {game.history().map((move, index) => (
                          <div key={index} className={`px-2 py-1 rounded ${index % 2 === 0 ? 'bg-muted' : ''}`}>
                            {index % 2 === 0 ? `${Math.floor(index / 2) + 1}. ` : ''}
                            {move}
                          </div>
                        ))}
                      </div>
                    )}
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
