# Introduction to Chess AI

Chess is one of the most popular board games in the world. It is a two-player strategy game that requires critical thinking, foresight, and planning. The game has a rich history and has been played for centuries. Chess has also been a popular subject for AI research, with many computer programs developed to play the game at a high level.

I choose chess as the subject for this course because it provides a rich environment for exploring AI techniques. Chess is a complex game with a large branching factor, which makes it challenging for traditional search algorithms. Chess also requires long-term planning and strategic thinking, which are essential skills for AI agents.

## 1. Chess Basics

### Board

The chessboard is an 8x8 grid with alternating light and dark squares. The rows are numbered from `1` to `8`, and the columns are labeled from `A` to `H`. Each square on the board is identified by a unique coordinate, such as `E4` or `G7`. The main diagonal color is always light as follows:

|   | A | B | C | D | E | F | G | H |  |
|---|---|---|---|---|---|---|---|---|--|
| 8 |⬜|⬛|⬜|⬛|⬜|⬛|⬜|⬛| 8 |
| 7 |⬛|⬜|⬛|⬜|⬛|⬜|⬛|⬜| 7 |
| 6 |⬜|⬛|⬜|⬛|⬜|⬛|⬜|⬛| 6 |
| 5 |⬛|⬜|⬛|⬜|⬛|⬜|⬛|⬜| 5 |
| 4 |⬜|⬛|⬜|⬛|⬜|⬛|⬜|⬛| 4 |
| 3 |⬛|⬜|⬛|⬜|⬛|⬜|⬛|⬜| 3 |
| 2 |⬜|⬛|⬜|⬛|⬜|⬛|⬜|⬛| 2 |
| 1 |⬛|⬜|⬛|⬜|⬛|⬜|⬛|⬜| 1 |
|   | A | B | C | D | E | F | G | H |  |

Initial board setup:

|   | A | B | C | D | E | F | G | H |  |
|---|---|---|---|---|---|---|---|---|--|
| 8 |♜|♞|♝|♛|♚|♝|♞|♜| 8 |
| 7 |♟|♟|♟|♟|♟|♟|♟|♟| 7 |
| 6 |⬜|⬛|⬜|⬛|⬜|⬛|⬜|⬛| 6 |
| 5 |⬛|⬜|⬛|⬜|⬛|⬜|⬛|⬜| 5 |
| 4 |⬜|⬛|⬜|⬛|⬜|⬛|⬜|⬛| 4 |
| 3 |⬛|⬜|⬛|⬜|⬛|⬜|⬛|⬜| 3 |
| 2 |♙|♙|♙|♙|♙|♙|♙|♙| 2 |
| 1 |♖|♘|♗|♕|♔|♗|♘|♖| 1 |
|   | A | B | C | D | E | F | G | H |  |

### Pieces

Chess has six different types of pieces:

- **Pawn** ♙️ / ♟: 
    - **Moves forward one square** in the common **general case**;
    - Can **move two squares** on its **first move**;
    - **Captures** opponents **diagonally** in one square.
    - **Promotes** to any other piece when reaching the opposite side of the board.
    - Can perform an **en passant** capture.
- **Rook** ♖ / ♜: 
    - **Moves horizontally** or **vertically** any number of squares.
    - **Castles** with the king under certain conditions.
- **Knight** ♘ / ♞: 
    - **Moves in an L-shape**: two squares in one direction and then one square perpendicular.
    - Can **jump over other pieces**.
- **Bishop** ♗ / ♝: 
    - **Moves diagonally** any number of squares.
    - Can only **stay on squares of the same color**.
- **Queen** ♕ / ♛: 
    - **Moves horizontally**, **vertically**, or **diagonally** any number of squares.
- **King** ♔ / ♚: 
    - **Moves one square** in any direction.
    - Can **castle** with the rook under certain conditions.
    - Cannot move into check squares.

### Rules

**Capture**: No piece can move through another piece, but it can capture an opponent's piece by moving to its square.

**Check**: is a situation where the king is under threat of capture. The player must make a move to remove the check such as blocking the attack, moving the king, or capturing the attacking piece.

**Checkmate**: the king is in check and cannot escape. The game ends, and the player who delivered the checkmate wins.

**En passant**: a special pawn capture that allows a pawn that has moved two squares forward from its starting position to be captured by an opponent's pawn that is on an adjacent file and on the same rank.

**Stalemate**: the player has no legal moves, and their king is not in check. The game ends in a draw.

**50-move rule**: if no pawn has moved and no capture has been made in the last 50 moves, the game can be declared a draw.

**Threefold repetition**: if the same position occurs three times with the same player to move, the game can be declared a draw.
