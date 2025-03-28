# Chess data structures

Before we dive into the algorithms, we need to define the data structures that will represent the chessboard and the pieces. In this section, we will discuss how to represent the chessboard, the pieces, and their movements.

::: note "Chess Data Structures"
There are several ways to represent the chessboard and pieces in a computer program. The choice of representation can affect the efficiency of the algorithms we will implement later. 
:::

Alternatively you can use a combination of data structures and strategies to represent the chessboard, pieces and other useful information. In general, you can use board centric as 8x8, vector attack or piece centric as Piece-Lists, Piece-Sets, Piece-Maps, or bitboards. We wont cover them in this course, but you can find more information in the [Chess Programming Wiki](https://www.chessprogramming.org/Main_Page).

## Pieces

``` c++
enum class PieceType: std::uint8_t {
    EMPTY  = 0b000,
    PAWN   = 0b001,
    KNIGHT = 0b010,
    BISHOP = 0b011,
    ROOK   = 0b100,
    QUEEN  = 0b101,
    KING   = 0b110
};
```

## Piece Color

``` c++
enum class PieceColor: std::uint8_t {
    BLACK = 0b0,
    WHITE = 0b1
};
```

## Square Content

``` c++
/**
 * @brief Represents a piece on the board
 * @details 4 bits. the first bit is the color. the last 3 bits are the piece type. if all of them are 0, it is an empty square
 */
enum class SquareContent: std::uint8_t {
    EMPTY        = 0b0000,
    BLACK_PAWN   = 0b0001,
    BLACK_KNIGHT = 0b0010,
    BLACK_BISHOP = 0b0011,
    BLACK_ROOK   = 0b0100,
    BLACK_QUEEN  = 0b0101,
    BLACK_KING   = 0b0110,
    WHITE_PAWN   = 0b1001,
    WHITE_KNIGHT = 0b1010,
    WHITE_BISHOP = 0b1011,
    WHITE_ROOK   = 0b1100,
    WHITE_QUEEN  = 0b1101,
    WHITE_KING   = 0b1110
};
```

## Square Content with Color

``` c++
struct Piece {
    PieceColor color:1;
    PieceType  type:3;
};
```