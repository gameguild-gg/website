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
            std::cout << "CATERROR - CAT made invalid Move: " << catMove.x << " " << catMove.y << std::endl;
            return 0;
        }
        board.catPos = catMove;
        board.turn = Simulator::Turn::CATCHER;
        if(board.CatWon())
            printWithTime(board, "CATWIN", elapsed.count(), "CAT MOVE: " + std::to_string(catMove.x) + " " + std::to_string(catMove.y));
        else
            printWithTime(board, "CATCHER", elapsed.count(), "CAT MOVE: " + std::to_string(catMove.x) + " " + std::to_string(catMove.y));
    } else if (turn == Simulator::Turn::CATCHER) {
        Catcher catcher;
        std::pair catPos(catX, catY);
        auto start = std::chrono::high_resolution_clock::now();
        Simulator::Position catcherMove = catcher.move(board.blocked, board.catPos.toPair(), board.sideSize);
        auto elapsed = duration_cast<std::chrono::microseconds>(std::chrono::high_resolution_clock::now() - start);
        if(!board.CatcherCanMoveToPosition(catcherMove)){
            std::cout << "CATCHERERROR - CATCHER made invalid Move: " << catcherMove.x << " " << catcherMove.y << std::endl;
            return 0;
        }
        board.blocked[(catcherMove.y + sideSize/2) * sideSize + catcherMove.x+sideSize/2] = true;
        board.turn = Simulator::Turn::CAT;
        if(board.CatcherWon())
            printWithTime(board, "CATHERWIN", elapsed.count(), "CATCHER MOVE: " + std::to_string(catcherMove.x) + " " + std::to_string(catcherMove.y));
        else
            printWithTime(board, "CAT", elapsed.count(), "CATCHER MOVE: " + std::to_string(catcherMove.x) + " " + std::to_string(catcherMove.y));
    } else {
        std::cout << "Invalid turn" << std::endl;
    }
}