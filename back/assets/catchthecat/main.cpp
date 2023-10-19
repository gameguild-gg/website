#include "functions.h"
#include "Cat.h"
#include "Catcher.h"

int main() {
    string turn;
    int sideSize;
    int catX, catY;
    vector<bool> blocked;
    cin >> turn >> sideSize >> catX >> catY;
    blocked = readBoard(sideSize);
    if(turn == "CAT") {
        Cat cat;
        pair catPos(catX, catY);
        auto start = std::chrono::high_resolution_clock::now();
        Position catMove = cat.move(blocked, catPos, sideSize);
        auto elapsed = std::chrono::high_resolution_clock::now() - start;
        if(CatWon(Board(blocked, sideSize, catPos), catMove))
            printWithoutTime(blocked, sideSize, catMove, "CATWIN");
        // test againt bad move
        else
            printWithoutTime(blocked, sideSize, catMove, "CATCHER");
    } else if (turn == "CATCHER") {
        Catcher catcher;
        pair catPos(catX, catY);
        auto start = std::chrono::high_resolution_clock::now();
        Position catcherMove = catcher.move(blocked, catPos, sideSize);
        auto elapsed = std::chrono::high_resolution_clock::now() - start;
        blocked[(catcherMove.y + sideSize/2) * sideSize + catcherMove.x+sideSize/2] = true;
        if(CatcherWon(Board(blocked, sideSize, {catX, catY}), catcherMove))
            printWithoutTime(blocked, sideSize, catcherMove, "CATHERWIN");
        // test againt bad move
        else
            printWithoutTime(blocked, sideSize, catcherMove, "CAT");
    } else {
        cout << "Invalid turn" << endl;
    }
}