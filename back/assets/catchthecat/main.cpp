#include "simulator.h"
#include "Cat.h"
#include "Catcher.h"
#include <chrono>
#include <iostream>

int main(int argc, char** argv) {
    string turnInput;
    int sideSize;
    int catX, catY;
    cin >> turnInput >> sideSize >> catX >> catY;
    auto turn = getTurn(turnInput);
    pair catPos(catX, catY);
    auto board = Board(readBoard(sideSize), sideSize, catPos);
    if(turn == Turn::CAT) {
        Cat cat;

        auto start = std::chrono::high_resolution_clock::now();
        Position catMove = cat.move(board.blocked, board.catPos.toPair(), board.sideSize);
        auto elapsed = duration_cast<std::chrono::microseconds>(std::chrono::high_resolution_clock::now() - start);

        if(!board.CatCanMoveToPosition(catMove)){
            cout << "CATHERWIN - CAT made invalid Move" << endl;
            exit(-1);
        }
        board.catPos = catMove;
        board.turn = Turn::CATCHER;
        if(board.CatWon())
            printWithTime(board, "CATWIN", elapsed.count());
        else
            printWithTime(board, "CATCHER", elapsed.count());
    } else if (turn == Turn::CATCHER) {
        Catcher catcher;
        pair catPos(catX, catY);
        auto start = std::chrono::high_resolution_clock::now();
        Position catcherMove = catcher.move(board.blocked, board.catPos.toPair(), board.sideSize);
        auto elapsed = duration_cast<std::chrono::microseconds>(std::chrono::high_resolution_clock::now() - start);
        if(!board.CatcherCanMoveToPosition(catcherMove)){
            cout << "CATWIN - CATCHER made invalid Move" << endl;
            exit(-1);
        }
        board.blocked[(catcherMove.y + sideSize/2) * sideSize + catcherMove.x+sideSize/2] = true;
        board.turn = Turn::CAT;
        if(board.CatcherWon())
            printWithTime(board, "CATHERWIN", elapsed.count());
        else
            printWithTime(board, "CAT", elapsed.count());
    } else {
        cout << "Invalid turn" << endl;
    }
}