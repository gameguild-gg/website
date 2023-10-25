#ifndef CAT_h
#define CAT_h
#include "IAgent.h"
#include "simulator.h"

struct Cat : public IAgent {
  std::pair<int,int> move(const std::vector<bool>& world, std::pair<int,int> catPos, int sideSize ) override{
      Simulator::Board board(world, sideSize, catPos);
      auto path = buildPath(board);
      if(path.empty())
          return board.NeighborsInsideBoundariesNotBlocked(catPos)[0].toPair();
      else
          return path.back().toPair();
  }
};
#endif