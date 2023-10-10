#ifndef CATCHER_H
#define CATCHER_H
#include "IAgent.h"

struct Catcher : public IAgent {
  std::pair<int,int> move(const std::vector<bool>& world, std::pair<int,int> catPos, int sideSize ) override{
    return {0,0}; // todo: change this
  }
};
#endif