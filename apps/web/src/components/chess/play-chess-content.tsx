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
import { Api, CompetitionsApi } from '@game-guild/apiclient';
import { getSession } from 'next-auth/react';
import { GetSessionReturnType } from '@/configs/auth.config';
import ChessAgentResponseEntryDto = Api.ChessAgentResponseEntryDto;

type PlayerType = 'human' | 'bot';

interface PlayerConfig {
  type: PlayerType;
  botId: string | null;
}

// Add enum for game result types
enum GameResultType {
  Checkmate = 'checkmate',
  Stalemate = 'stalemate',
  InsufficientMaterial = 'insufficient_material',
  FiftyMoveRule = 'fifty_move_rule',
  ThreefoldRepetition = 'threefold_repetition',
  InvalidMove = 'invalid_move',
  Unknown = 'unknown',
}

// Add interface for game result
interface GameResult {
  type: GameResultType;
  winner?: 'white' | 'black';
  reason?: string;
  botColor?: 'white' | 'black';
  attemptedMove?: string;
}

export default function PlayChessContent() {
  // Board state
  const [game, setGame] = useState<Chess>(new Chess());
  const [fen, setFen] = useState<string>(game.fen());
  const [isGameOver, setIsGameOver] = useState<boolean>(false);
  const [gameResult, setGameResult] = useState<GameResult | null>(null);
  const [lastMove, setLastMove] = useState<{ from: string; to: string } | null>(null);
  const [moveHistory, setMoveHistory] = useState<string[]>([]);
  const [halfmoveClock, setHalfmoveClock] = useState<number>(0);

  // Sync game state when fen changes
  useEffect(() => {
    // Only update if the fen has changed from the current game state
    if (fen !== game.fen()) {
      game.load(fen);

      // Update the halfmove clock from the FEN
      const fenParts = fen.split(' ');
      if (fenParts.length >= 5) {
        const halfmoveValue = parseInt(fenParts[4]);
        console.log('Extracted halfmove clock:', halfmoveValue, 'from FEN:', fen);
        setHalfmoveClock(halfmoveValue);
      }

      // Check for all draw conditions explicitly
      const isDrawByFiftyMoveRule = fenParts.length >= 5 && parseInt(fenParts[4]) >= 50;
      const isDrawByThreefoldRepetition = game.isThreefoldRepetition();
      const isDrawByInsufficientMaterial = game.isInsufficientMaterial();
      const isDrawByStalemate = game.isStalemate();

      // Check if the game is over, including all types of draws
      if (game.isGameOver() || game.isDraw() || isDrawByFiftyMoveRule || isDrawByThreefoldRepetition || isDrawByInsufficientMaterial || isDrawByStalemate) {
        console.log(
          'Game over detected:',
          isDrawByFiftyMoveRule
            ? '50-move rule'
            : isDrawByThreefoldRepetition
              ? 'threefold repetition'
              : isDrawByInsufficientMaterial
                ? 'insufficient material'
                : isDrawByStalemate
                  ? 'stalemate'
                  : 'other reason',
        );
        setIsGameOver(true);
        setGameResult(getGameResult(game));
        setGameStarted(false);
      }
    }
  }, [fen, game]);

  // Player configuration
  const [whitePlayer, setWhitePlayer] = useState<PlayerConfig>({ type: 'human', botId: null });
  const [blackPlayer, setBlackPlayer] = useState<PlayerConfig>({ type: 'bot', botId: null });

  // UI state
  const [availableBots, setAvailableBots] = useState<ChessAgentResponseEntryDto[]>([]);
  const [isLoading, setIsLoading] = useState<boolean>(true);
  const [isBotThinking, setIsBotThinking] = useState<boolean>(false);
  const [error, setError] = useState<string | null>(null);
  const [autoPlay, setAutoPlay] = useState<boolean>(true);
  const [gameStarted, setGameStarted] = useState<boolean>(false);

  // Ref for the board container and board width state
  const boardContainerRef = useRef<HTMLDivElement>(null);
  const [boardWidth, setBoardWidth] = useState(450);

  const api = new CompetitionsApi({
    basePath: process.env.NEXT_PUBLIC_API_URL,
  });

  // Helper function to determine game result
  function getGameResult(chess: Chess): GameResult {
    // Check for checkmate
    if (chess.isCheckmate()) {
      return {
        type: GameResultType.Checkmate,
        winner: chess.turn() === 'w' ? 'black' : 'white',
      };
    }

    // Check for stalemate
    if (chess.isStalemate()) {
      return {
        type: GameResultType.Stalemate,
        reason: 'No legal moves available',
      };
    }

    // Check for insufficient material
    if (chess.isInsufficientMaterial()) {
      return {
        type: GameResultType.InsufficientMaterial,
        reason: 'Insufficient material to checkmate',
      };
    }

    // Check for threefold repetition
    if (chess.isThreefoldRepetition()) {
      return {
        type: GameResultType.ThreefoldRepetition,
        reason: 'Same position occurred three times',
      };
    }

    // Check for 50-move rule explicitly
    const fenParts = chess.fen().split(' ');
    if (fenParts.length >= 5 && parseInt(fenParts[4]) >= 50) {
      return {
        type: GameResultType.FiftyMoveRule,
        reason: '50 moves without pawn move or capture',
      };
    }

    // Generic draw check (catches other draw conditions)
    if (chess.isDraw()) {
      // Try to determine the specific draw type
      if (chess.isInsufficientMaterial()) {
        return { type: GameResultType.InsufficientMaterial, reason: 'Insufficient material to checkmate' };
      } else if (chess.isThreefoldRepetition()) {
        return { type: GameResultType.ThreefoldRepetition, reason: 'Same position occurred three times' };
      } else if (fenParts.length >= 5 && parseInt(fenParts[4]) >= 50) {
        return { type: GameResultType.FiftyMoveRule, reason: '50 moves without pawn move or capture' };
      } else if (chess.isStalemate()) {
        return { type: GameResultType.Stalemate, reason: 'No legal moves available' };
      }

      // Fallback for other draw types - changed from FiftyMoveRule to Unknown with a generic reason
      return { type: GameResultType.Unknown, reason: 'Draw condition met' };
    }

    return { type: GameResultType.Unknown };
  }

  // Handle invalid bot move
  const handleInvalidBotMove = useCallback(
    (botColor: 'white' | 'black', moveString: string) => {
      // Determine the winner (opposite of the bot that made the invalid move)
      const winner = botColor === 'white' ? 'black' : 'white';

      // End the game and set the result
      setIsGameOver(true);
      setGameResult({
        type: GameResultType.InvalidMove,
        winner: winner,
        botColor: botColor,
        attemptedMove: moveString,
      });
      setGameStarted(false);

      console.error(
        `Invalid bot move: ${botColor} bot (${botColor === 'white' ? whitePlayer.botId : blackPlayer.botId}) attempted illegal move: ${moveString}`,
      );
    },
    [whitePlayer.botId, blackPlayer.botId],
  );

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

        // Set default bots
        if (data.length > 0) {
          setBlackPlayer({ type: 'bot', botId: data[0].username });
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
  const makeBotMove = useCallback(
    async (currentFen: string, username: string) => {
      if (!username) return;

      setIsBotThinking(true);

      try {
        const session = await getSession();
        console.log('Requesting move for', username, 'with FEN:', currentFen);

        try {
          // Use the API client
          const response = await api.competitionControllerRequestChessMove(
            {
              fen: currentFen,
              username: username,
            },
            {
              headers: {
                Authorization: `Bearer ${session?.user?.accessToken}`,
              },
            },
          );

          console.log('Bot move response status:', response.status);

          // Handle different response statuses
          if (response.status === 401) {
            setError('You are not authorized to view this page.');
            return null;
          }

          if (response.status === 500) {
            setError('Internal server error. Please try again later.');
            console.error(JSON.stringify(response.body));
            return null;
          }

          // For status 201 (Created) or 200 (OK), process the move
          if (response.status === 201 || response.status === 200) {
            // Extract the move string from the response body
            const moveString = response.body as string;
            console.log('Received move:', moveString);

            if (moveString) {
              // Determine the bot's color
              const botColor = game.turn() === 'w' ? 'white' : 'black';

              try {
                // Apply the move to the current game
                const moveResult = game.move(moveString);

                if (!moveResult) {
                  // Handle invalid move
                  handleInvalidBotMove(botColor, moveString);
                  return null;
                }

                console.log('Move applied successfully:', moveResult);

                // Update the game state
                const newFen = game.fen();
                setFen(newFen);
                setMoveHistory(game.history());
                setLastMove({
                  from: moveResult.from,
                  to: moveResult.to,
                });

                // Update halfmove clock from the new FEN
                const fenParts = newFen.split(' ');
                if (fenParts.length >= 5) {
                  const halfmoveValue = parseInt(fenParts[4]);
                  console.log('Updated halfmove clock after bot move:', halfmoveValue);
                  setHalfmoveClock(halfmoveValue);
                }

                // Check for all draw conditions explicitly
                const isDrawByFiftyMoveRule = fenParts.length >= 5 && parseInt(fenParts[4]) >= 50;
                const isDrawByThreefoldRepetition = game.isThreefoldRepetition();
                const isDrawByInsufficientMaterial = game.isInsufficientMaterial();
                const isDrawByStalemate = game.isStalemate();

                // Check if the game is over, including all types of draws
                if (
                  game.isGameOver() ||
                  game.isDraw() ||
                  isDrawByFiftyMoveRule ||
                  isDrawByThreefoldRepetition ||
                  isDrawByInsufficientMaterial ||
                  isDrawByStalemate
                ) {
                  console.log(
                    'Game over detected after bot move:',
                    isDrawByFiftyMoveRule
                      ? '50-move rule'
                      : isDrawByThreefoldRepetition
                        ? 'threefold repetition'
                        : isDrawByInsufficientMaterial
                          ? 'insufficient material'
                          : isDrawByStalemate
                            ? 'stalemate'
                            : 'other reason',
                  );
                  setIsGameOver(true);
                  setGameResult(getGameResult(game));
                  setGameStarted(false);
                }

                return moveResult;
              } catch (moveError) {
                // Handle invalid move (syntax error or other chess.js error)
                handleInvalidBotMove(botColor, moveString);
                return null;
              }
            } else {
              throw new Error('No move received from server');
            }
          } else {
            throw new Error(`Unexpected response status: ${response.status}`);
          }
        } catch (apiError) {
          console.error('Error handling API response:', apiError);
          setError('Failed to get bot move. Please try again.');
          return null;
        }
      } catch (err) {
        console.error('Error getting bot move:', err);
        setError('Failed to get bot move. Please try again.');
        return null;
      } finally {
        setIsBotThinking(false);
      }
    },
    [game, handleInvalidBotMove],
  );

  // Auto-play logic for bot vs bot games
  useEffect(() => {
    if (!gameStarted || isGameOver || !autoPlay) return;

    const currentTurn = game.turn() === 'w' ? 'white' : 'black';
    const currentPlayer = currentTurn === 'white' ? whitePlayer : blackPlayer;

    if (currentPlayer.type === 'bot' && currentPlayer.botId && !isBotThinking) {
      makeBotMove(game.fen(), currentPlayer.botId!);
    }
  }, [game, whitePlayer, blackPlayer, autoPlay, gameStarted, isGameOver, makeBotMove, isBotThinking]);

  // Handle human moves
  const onDrop = (sourceSquare: string, targetSquare: string) => {
    if (isGameOver || isBotThinking) return false;

    // If game hasn't started yet, automatically start it when a move is made
    if (!gameStarted) {
      setGameStarted(true);
      setError(null);
    }

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
      const newFen = game.fen();
      setFen(newFen);
      setMoveHistory(game.history());
      setLastMove({ from: sourceSquare, to: targetSquare });

      // Update halfmove clock from the new FEN
      const fenParts = newFen.split(' ');
      if (fenParts.length >= 5) {
        const halfmoveValue = parseInt(fenParts[4]);
        console.log('Updated halfmove clock after human move:', halfmoveValue);
        setHalfmoveClock(halfmoveValue);
      }

      // Check for all draw conditions explicitly
      const isDrawByFiftyMoveRule = fenParts.length >= 5 && parseInt(fenParts[4]) >= 50;
      const isDrawByThreefoldRepetition = game.isThreefoldRepetition();
      const isDrawByInsufficientMaterial = game.isInsufficientMaterial();
      const isDrawByStalemate = game.isStalemate();

      // Check if the game is over, including all types of draws
      if (game.isGameOver() || game.isDraw() || isDrawByFiftyMoveRule || isDrawByThreefoldRepetition || isDrawByInsufficientMaterial || isDrawByStalemate) {
        console.log(
          'Game over detected after human move:',
          isDrawByFiftyMoveRule
            ? '50-move rule'
            : isDrawByThreefoldRepetition
              ? 'threefold repetition'
              : isDrawByInsufficientMaterial
                ? 'insufficient material'
                : isDrawByStalemate
                  ? 'stalemate'
                  : 'other reason',
        );
        setIsGameOver(true);
        setGameResult(getGameResult(game));
        setGameStarted(false);
        return true;
      }

      // If the next player is a bot and auto-play is enabled, make the bot move
      const nextTurn = game.turn() === 'w' ? 'white' : 'black';
      const nextPlayer = nextTurn === 'white' ? whitePlayer : blackPlayer;

      if (nextPlayer.type === 'bot' && nextPlayer.botId && autoPlay) {
        makeBotMove(game.fen(), nextPlayer.botId!);
      }

      return true;
    } catch (error) {
      console.error('Error making move:', error);
      return false;
    }
  };

  // Start a new game
  const startNewGame = () => {
    // Reset the game to initial position
    game.reset();

    // Update state
    setFen(game.fen());
    setMoveHistory([]);
    setIsGameOver(false);
    setGameResult(null);
    setLastMove(null);
    setGameStarted(true);
    setError(null);
    setHalfmoveClock(0);

    // If white player is a bot and auto-play is enabled, make the first move
    if (whitePlayer.type === 'bot' && whitePlayer.botId && autoPlay) {
      makeBotMove(game.fen(), whitePlayer.botId!);
    }
  };

  // Reset the game
  const resetGame = () => {
    // Reset the game to initial position
    game.reset();

    // Update state
    setFen(game.fen());
    setMoveHistory([]);
    setIsGameOver(false);
    setGameResult(null);
    setLastMove(null);
    setGameStarted(false);
    setError(null);
    setHalfmoveClock(0);
  };

  // Make a single bot move (for manual play)
  const makeNextBotMove = async () => {
    if (isGameOver || isBotThinking) return;

    const currentTurn = game.turn() === 'w' ? 'white' : 'black';
    const currentPlayer = currentTurn === 'white' ? whitePlayer : blackPlayer;

    if (currentPlayer.type === 'bot' && currentPlayer.botId) {
      console.log('Making next bot move for', currentPlayer.botId);
      try {
        const result = await makeBotMove(game.fen(), currentPlayer.botId);
        console.log('Bot move result:', result);
      } catch (error) {
        console.error('Error in makeNextBotMove:', error);
        setError('Failed to make bot move. Please try again.');
      }
    }
  };

  // Format the game result for display
  const formatGameResult = () => {
    if (!gameResult) return null;

    switch (gameResult.type) {
      case GameResultType.Checkmate:
        return `Checkmate! ${gameResult.winner === 'white' ? 'White' : 'Black'} wins.`;

      case GameResultType.Stalemate:
        return `Stalemate! The game is a draw. ${game.turn() === 'w' ? 'White' : 'Black'} has no legal moves but is not in check.`;

      case GameResultType.InsufficientMaterial:
        return `Draw by insufficient material. Neither player has enough pieces to checkmate.`;

      case GameResultType.FiftyMoveRule:
        return `Draw by 50-move rule. There have been 50 consecutive moves without a pawn move or capture.`;

      case GameResultType.ThreefoldRepetition:
        return `Draw by threefold repetition. The same position has occurred three times.`;

      case GameResultType.InvalidMove:
        return `Invalid move! The ${gameResult.botColor} bot attempted an illegal move (${gameResult.attemptedMove}). ${gameResult.winner === 'white' ? 'White' : 'Black'} wins by forfeit.`;

      default:
        return 'Game over.';
    }
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
                  <Alert
                    className={`mb-4 ${
                      gameResult.type === GameResultType.Stalemate ||
                      gameResult.type === GameResultType.InsufficientMaterial ||
                      gameResult.type === GameResultType.FiftyMoveRule ||
                      gameResult.type === GameResultType.ThreefoldRepetition
                        ? 'bg-amber-50 border-amber-200 dark:bg-amber-950/20 dark:border-amber-800'
                        : gameResult.type === GameResultType.InvalidMove
                          ? 'bg-red-50 border-red-200 dark:bg-red-950/20 dark:border-red-800'
                          : ''
                    }`}
                  >
                    {gameResult.type === GameResultType.Stalemate ||
                    gameResult.type === GameResultType.InsufficientMaterial ||
                    gameResult.type === GameResultType.FiftyMoveRule ||
                    gameResult.type === GameResultType.ThreefoldRepetition ? (
                      <AlertTriangle className="h-4 w-4 text-amber-600 dark:text-amber-400" />
                    ) : gameResult.type === GameResultType.InvalidMove ? (
                      <AlertTriangle className="h-4 w-4 text-red-600 dark:text-red-400" />
                    ) : (
                      <Trophy className="h-4 w-4" />
                    )}
                    <AlertTitle>
                      {gameResult.type === GameResultType.Stalemate
                        ? 'Stalemate'
                        : gameResult.type === GameResultType.InsufficientMaterial
                          ? 'Insufficient Material'
                          : gameResult.type === GameResultType.FiftyMoveRule
                            ? '50-Move Rule'
                            : gameResult.type === GameResultType.ThreefoldRepetition
                              ? 'Threefold Repetition'
                              : gameResult.type === GameResultType.InvalidMove
                                ? 'Invalid Bot Move'
                                : 'Game Over'}
                    </AlertTitle>
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
                              botId: checked ? null : availableBots[0]?.username || null,
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
                            <SelectItem key={bot.username} value={bot.username}>
                              {bot.username} (ELO: {bot.elo})
                            </SelectItem>
                          ))}
                        </SelectContent>
                      </Select>
                    )}

                    {whitePlayer.type === 'bot' && whitePlayer.botId && (
                      <div className="text-sm text-muted-foreground">ELO: {availableBots.find((b) => b.username === whitePlayer.botId)?.elo || 'N/A'}</div>
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
                              botId: checked ? null : availableBots[0]?.username || null,
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
                            <SelectItem key={bot.username} value={bot.username}>
                              {bot.username} (ELO: {bot.elo})
                            </SelectItem>
                          ))}
                        </SelectContent>
                      </Select>
                    )}

                    {blackPlayer.type === 'bot' && blackPlayer.botId && (
                      <div className="text-sm text-muted-foreground">ELO: {availableBots.find((b) => b.username === blackPlayer.botId)?.elo || 'N/A'}</div>
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
                      <span>{whitePlayer.type === 'human' ? 'Human Player' : whitePlayer.botId}</span>
                    </div>
                    <div className="flex justify-between">
                      <span className="text-sm font-medium">Black:</span>
                      <span>{blackPlayer.type === 'human' ? 'Human Player' : blackPlayer.botId}</span>
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
                    <div className="flex justify-between">
                      <span className="text-sm font-medium">Halfmove Clock:</span>
                      <span className={halfmoveClock >= 40 ? 'text-amber-600 font-medium' : ''}>{halfmoveClock}/50</span>
                    </div>
                    <div className="flex justify-between">
                      <span className="text-sm font-medium">Draw Status:</span>
                      <span>
                        {halfmoveClock >= 50 || (gameResult && gameResult.type === GameResultType.FiftyMoveRule) ? (
                          <Badge variant="outline" className="bg-amber-50 text-amber-800 border-amber-300 dark:bg-amber-950 dark:text-amber-300">
                            50-move rule applies
                          </Badge>
                        ) : game.isInsufficientMaterial() || (gameResult && gameResult.type === GameResultType.InsufficientMaterial) ? (
                          <Badge variant="outline" className="bg-amber-50 text-amber-800 border-amber-300 dark:bg-amber-950 dark:text-amber-300">
                            Insufficient material
                          </Badge>
                        ) : game.isThreefoldRepetition() || (gameResult && gameResult.type === GameResultType.ThreefoldRepetition) ? (
                          <Badge variant="outline" className="bg-amber-50 text-amber-800 border-amber-300 dark:bg-amber-950 dark:text-amber-300">
                            Threefold repetition
                          </Badge>
                        ) : game.isStalemate() || (gameResult && gameResult.type === GameResultType.Stalemate) ? (
                          <Badge variant="outline" className="bg-amber-50 text-amber-800 border-amber-300 dark:bg-amber-950 dark:text-amber-300">
                            Stalemate
                          </Badge>
                        ) : (
                          <span className="text-muted-foreground">None</span>
                        )}
                      </span>
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
                    {moveHistory.length === 0 ? (
                      <div className="text-center text-muted-foreground py-4">No moves yet</div>
                    ) : (
                      <div className="space-y-2">
                        {/* Group moves by turn and display in reverse order */}
                        {(() => {
                          // Create an array of turns
                          const turns: { number: number; white: string | null; black: string | null }[] = [];

                          // Group moves into turns
                          for (let i = 0; i < moveHistory.length; i += 2) {
                            const turnNumber = Math.floor(i / 2) + 1;
                            turns.push({
                              number: turnNumber,
                              white: moveHistory[i] || null,
                              black: i + 1 < moveHistory.length ? moveHistory[i + 1] : null,
                            });
                          }

                          // Reverse the turns array to display latest first
                          return turns.reverse().map((turn) => (
                            <div key={turn.number} className="flex bg-muted/30 rounded p-1">
                              <div className="w-10 font-medium text-muted-foreground">{turn.number}.</div>
                              <div className="flex-1 px-2">{turn.white}</div>
                              <div className="flex-1 px-2">{turn.black}</div>
                            </div>
                          ));
                        })()}
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
