#ifndef CAT_h
#define CAT_h
#include "IAgent.h"
#include "functions.h"

struct Cat : public IAgent {
  std::pair<int,int> move(const std::vector<bool>& world, std::pair<int,int> catPos, int sideSize ) override{
      Position inputPos = catPos;
      auto options = emptyNeighbors(world, sideSize, inputPos);
      auto random = Range(0, options.size()-1);
        return options[random].toPair();
  }
};
#endif