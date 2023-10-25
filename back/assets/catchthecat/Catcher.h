#ifndef CATCHER_H
#define CATCHER_H
#include "IAgent.h"
#include "simulator.h"

struct Catcher : public IAgent {
  std::pair<int,int> move(const std::vector<bool>& world, std::pair<int,int> catPos, int sideSize ) override {
    Board board(world, sideSize, catPos);
    auto path = buildPath(board);
    if(path.empty())
        return board.NeighborsInsideBoundariesNotBlocked(catPos)[0].toPair();
    else
        return path[0].toPair();
  }
};
#endif