# Min Max Search

<details>
<summary>Lecture Notes</summary>

- Heuristics
- Min Max Search
- Alpha Beta Pruning

</details>

Video explanation: [Min Max Search](https://www.youtube.com/watch?v=l-hh51ncgDI) and this [other one](https://www.youtube.com/watch?v=trKjYdBASyQ)

Min Max Search is a search algorithm used in two-player turn based adversarial games to find the optimal move for the player. It works by exploring all possible moves and their outcomes, and selecting the move that maximizes the player's chances of winning while minimizing the opponent's chances. The algorithm assumes that both players play optimally.

## Heuristics

Heuristics are rules of thumb or strategies that help us make decisions or solve problems more efficiently. Heuristics are functions which will feed on the state and generate a score.

In the context of Min Max Search, heuristics are used to evaluate the desirability of a game state. They provide a way to estimate the value of a position without having to search all possible moves.

In chess, heuristics can be:

- **Material balance**: The difference in value between the pieces on the board. For example, Queen is worth 9 points, Rook: 5, Bishop and Knight: 3 points, and Pawn: 1 point. 
- **Pawn Structure**: The arrangement of pawns on the board. Isolated, passed, chained, backward, doubled, tripled, quadrupled. Open files, closed files, half open files.
- **King Safety**: The safety of the king. Is it exposed to attacks? Is it well protected?
- **Space**: The amount of space controlled by the pieces. More space means more mobility and more options.
- **Weak/Strong Squares**: The squares that are weak or strong for each player. Strong squares are squares that can be occupied by pieces and weak squares are squares that can be attacked.
- **Development**: The number of pieces that are developed and active. More developed pieces mean more options and more control over the board.
- **Control of the center**: The control of the center squares. Controlling the center means more mobility and more options.
- **Initiative**: The player who has the initiative is the one who is forcing the opponent to react. The player with the initiative has more options and more control over the game.

We need to evaluate the board in order to generate a score, so we can look ahead and search for the best move.

## Min Max Search

The Min Max Search algorithm works by recursively exploring all possible moves and by evaluating the heuristics, their probable outcomes. 

In a min max search tree, the root node represents the current state of the game. The child nodes represent all possible moves that can be made from that state, and so on.

The naive implementation of Min Max Search is to clamp the maximum depth of the search tree. This means that we will only search a certain number of moves ahead. The deeper we go, the more accurate the evaluation will be, but the more time it will take to compute.

Let's assume a depth of 3 and all possible moves which can be achievable up to 3 moves ahead. The tree will look like this:

``` mermaid
graph TD;
    A[Root] --> A0[Min]
    A --> A1[Min]
    A0 --> A00[Max]
    A0 --> A01[Max]
    A1 --> A10[Max]
    A1 --> A11[Max]
    A00 --> A000[Leaf]
    A00 --> A001[Leaf]
    A01 --> A010[Leaf]
    A01 --> A011[Leaf]
    A10 --> A100[Leaf]
    A10 --> A101[Leaf]
    A11 --> A110[Leaf]
    A11 --> A111[Leaf]
    
style A0 fill:#880000
style A1 fill:#880000
style A00 fill:#008800
style A01 fill:#008800
style A10 fill:#008800
style A11 fill:#008800
```

The task is to evaluate the board on the leaf nodes and propagate the values up to the root node. The Min Max Search algorithm will select the maximum value for the player and the minimum value for the opponent.

Let's assume we have this tree:

``` mermaid
graph TD;
    A[Root] --> A0[Min]
    A --> A1[Min]
    A0 --> A00[Max]
    A0 --> A01[Max]
    A1 --> A10[Max]
    A1 --> A11[Max]
    A00 --> A000[-2]
    A00 --> A001[3]
    A01 --> A010[8]
    A01 --> A011[2]
    A10 --> A100[-8]
    A10 --> A101[-3]
    A11 --> A110[1]
    A11 --> A111[10]
    
style A0 fill:#880000
style A1 fill:#880000
style A00 fill:#008800
style A01 fill:#008800
style A10 fill:#008800
style A11 fill:#008800
```

And then we have to propagate the values up to the root node. Once we get the value on the root, the next movement will be the one that will lead to the maximum value. 

``` mermaid
flowchart TD
    A["Root"] --> A0["3"] & A1["-3"]
    A0 --> A00["3"] & A01["8"]
    A1 --> A10["-3"] & A11["10"]
    A00 --> A000["-2"] & A001["3"]
    A01 --> A010["8"] & A011["2"]
    A10 --> A100["-8"] & A101["-3"]
    A11 --> A110["1"] & A111["10"]

    style A0 fill:#880000
    style A1 fill:#880000
    style A00 fill:#008800
    style A01 fill:#008800
    style A10 fill:#008800
    style A11 fill:#008800
    linkStyle 0 stroke:#00C853
```