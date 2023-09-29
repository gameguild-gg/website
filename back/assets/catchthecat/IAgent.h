#ifndef IAgent_h
#define IAgent_h
#include <vector>
#include <utility>
#include <string>

// NO NOT CHANGE THIS FILE
struct IAgent {
public:
    /**
     * @brief the agent implementation. the center of the world is {0,0}, top left is {-sideSize/2, -sideSize/2} and the bottom right is {sideSize/2, sideSize/2}.
     *
     * @param world the world as a vector of booleans. true means there is a wall, false means there is no wall. The vector is the linearization of the matrix of the world.
     * @param catPos the position of the cat in the world {x,y} relative to the center of the world.
     * @param sideSize the side size of the world. it will be always a square that follows the sequence of 4*i+1: 5, 9, 13, 17, 21, 25, 29, 33, 37, 41, ...
     *
     * @return the position to move to {x,y}. relative to the center of the world.
     */
    virtual std::pair<int,int> move(const std::vector<bool>& world, std::pair<int,int> catPos, int sideSize ) = 0;
};
#endif