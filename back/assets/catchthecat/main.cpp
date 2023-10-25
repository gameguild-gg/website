#include "simulator.h"
#include "Cat.h"
#include "Catcher.h"
#include <chrono>
#include <iostream>

int main(int argc, char** argv) {
    std::string turnInput;
    int sideSize;
    int catX, catY;
    std::cin >> turnInput >> sideSize >> catX >> catY;
    auto turn = Simulator::getTurn(turnInput);
    std::pair catPos(catX, catY);
    auto board = Simulator::Board(Simulator::readBoard(sideSize), sideSize, catPos);
    if(turn == Simulator::Turn::CAT) {
        Cat cat;
        auto start = std::chrono::high_resolution_clock::now();
        Simulator::Position catMove = cat.move(board.blocked, board.catPos.toPair(), board.sideSize);
        auto elapsed = duration_cast<std::chrono::microseconds>(std::chrono::high_resolution_clock::now() - start);

        if(!board.CatCanMoveToPosition(catMove)){
            std::cout << "CATHERWIN - CAT made invalid Move" << std::endl;
            exit(-1);
        }
        board.catPos = catMove;
        board.turn = Simulator::Turn::CATCHER;
        if(board.CatWon())
            printWithTime(board, "CATWIN", elapsed.count());
        else
            printWithTime(board, "CATCHER", elapsed.count());
    } else if (turn == Simulator::Turn::CATCHER) {
        Catcher catcher;
        std::pair catPos(catX, catY);
        auto start = std::chrono::high_resolution_clock::now();
        Simulator::Position catcherMove = catcher.move(board.blocked, board.catPos.toPair(), board.sideSize);
        auto elapsed = duration_cast<std::chrono::microseconds>(std::chrono::high_resolution_clock::now() - start);
        if(!board.CatcherCanMoveToPosition(catcherMove)){
            std::cout << "CATWIN - CATCHER made invalid Move" << std::endl;
            exit(-1);
        }
        board.blocked[(catcherMove.y + sideSize/2) * sideSize + catcherMove.x+sideSize/2] = true;
        board.turn = Simulator::Turn::CAT;
        if(board.CatcherWon())
            printWithTime(board, "CATHERWIN", elapsed.count());
        else
            printWithTime(board, "CAT", elapsed.count());
    } else {
        std::cout << "Invalid turn" << std::endl;
    }
}