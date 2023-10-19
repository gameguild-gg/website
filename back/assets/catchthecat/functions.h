#ifndef functions_h
#define functions_h

#include <iostream>
#include <vector>
#include <string>
#include <random>
#include <queue>
#include <unordered_set>
#include <unordered_map>
#include <algorithm>

using namespace std;

struct Board;
struct Position;

void shuffleVector(vector<Position>& v){
    static std::random_device rd;
    static std::mt19937 rng(rd());
    std::shuffle(v.begin(), v.end(), rng);
}

int Range(int start, int end) {
    if (start == end) return start;
    static std::random_device rd;
    static std::mt19937 rng(rd());
    static std::uniform_int_distribution<int> dist(start, end);
    return dist(rng);
}

struct Position {
    int x;
    int y;

    Position(int x, int y): x(x), y(y) {}
    Position(): x(0), y(0) {}
    Position(const Position& p): x(p.x), y(p.y) {}
    Position(Position&& p) noexcept: x(p.x), y(p.y) {}
    Position(const pair<int, int>& p): x(p.first), y(p.second) {}
    Position& operator=(const Position& p)= default;
    Position& operator=(const pair<int, int>& p){
        x = p.first;
        y = p.second;
        return *this;
    }
    bool operator==(const Position& p) const{
        return x == p.x && y == p.y;
    }
    bool operator!=(const Position& p) const{
        return x != p.x || y != p.y;
    }
    inline Position NW() const{
        return (y % 2 == 0) ?
               Position(x-1, y-1) :
               Position(x, y-1);
    }
    inline Position NE() const{
        return (y % 2 == 0) ?
               Position(x, y-1) :
               Position(x+1, y-1);
    }
    inline Position E() const{
        return {x+1, y};
    }
    inline Position W() const{
        return {x-1, y};
    }
    inline Position SW() const{
        return (y % 2 == 0) ?
               Position(x-1, y+1) :
               Position(x, y+1);
    }
    inline Position SE() const{
        return (y % 2 == 0) ?
               Position(x, y+1) :
               Position(x+1, y+1);
    }
    inline bool IsInsideBoardBoundaries(int sideSize) const {
        return x >= -sideSize/2 && x <= sideSize/2 &&
               y >= -sideSize/2 && y <= sideSize/2;
    }
    inline vector<Position> Neighbors() const{
        return vector<Position>{NW(), NE(), E(), W(), SW(), SE()};
    }
    std::pair<int,int> toPair(){return {x,y};}

    uint64_t hash() const noexcept { return ((uint64_t)x) << 32 | (uint64_t)y; }
};

namespace std {
    template <> struct hash<Position> {
        std::size_t operator()(const Position& p) const noexcept { return p.hash(); }
    };
}  // namespace std

struct Board {
    vector<bool> blocked;
    int sideSize;
    Position catPos;

    Board(const Board& b) = default;
    Board(const vector<bool>& blocked, int sideSize, Position catPosition): blocked(blocked), sideSize(sideSize), catPos(catPosition) {}

    inline vector<Position> NeighborsInsideBoundaries(const Position& p) const{
        // filter Neighbors to only keep the ones inside the board boundaries
        vector<Position> neighbors = p.Neighbors();
        vector<Position> result;
        for(auto& neighbor : neighbors)
            if(neighbor.IsInsideBoardBoundaries(sideSize))
               result.push_back(neighbor);
        shuffleVector(result);
        return result;
    }
    vector<Position> NeighborsInsideBoundariesNotBlocked(const Position& p){
        vector<Position> neighbors = NeighborsInsideBoundaries(p);
        vector<Position> result;
        for(auto& neighbor : neighbors)
            if(!blocked[(neighbor.y + sideSize/2) * sideSize + neighbor.x + sideSize/2])
                result.push_back(neighbor);
        return result;
    }
};

std::vector<bool> readBoard(int sideSize) {
    std::vector<bool> board;
    board.reserve(sideSize*sideSize);
    for(int i=0; i<sideSize*sideSize; i++) {
        char c;
        std::cin >> c;
        switch (c) {
            case '#':
                board.push_back(true);
                break;
            case '.':
            case 'C':
                board.push_back(false);
                break;
            default:
                i--;
                break;
        }
    }
    return board;
}

vector<Position> emptyNeighbors(const std::vector<bool>& blocked, int sideSize, const Position& pos){
    vector<Position> empty;
    for(auto& neighbor : pos.Neighbors())
        if(neighbor.IsInsideBoardBoundaries(sideSize) && !blocked[(neighbor.y + sideSize/2) * sideSize + neighbor.x + sideSize/2])
            empty.push_back(neighbor);
    return empty;
}

void printWithTime(const std::vector<bool>& state, int sideSize, Position catPos, const std::string& turn, long long time){
    std::cout << turn << " " << sideSize << " " << catPos.x << " " << catPos.y << std::endl;
    catPos.x += sideSize/2;
    catPos.y += sideSize/2;
    auto catPosIndex = catPos.y * sideSize + catPos.x;
    for(int y=0; y<sideSize; y++) {
        if (y % 2 == 1) std::cout << ' ';
        for (int x = 0; x < sideSize; x++) {
            if(y * sideSize + x == catPosIndex) {
                std::cout << 'C';
            } else
                std::cout << (state[y * sideSize + x] ? '#' : '.');
            if (x < sideSize - 1) std::cout << ' ';
        }
        std::cout << std::endl;
    }
    cout << time << endl;
}

void printWithoutTime(const std::vector<bool>& state, int sideSize, Position catPos, const std::string& turn){
    std::cout << turn << " " << sideSize << " " << catPos.x << " " << catPos.y << std::endl;
    catPos.x += sideSize/2;
    catPos.y += sideSize/2;
    auto catPosIndex = catPos.y * sideSize + catPos.x;
    for(int y=0; y<sideSize; y++) {
        if (y % 2 == 1) std::cout << ' ';
        for (int x = 0; x < sideSize; x++) {
            if(y * sideSize + x == catPosIndex) {
                std::cout << 'C';
            } else
                std::cout << (state[y * sideSize + x] ? '#' : '.');
            if (x < sideSize - 1) std::cout << ' ';
        }
        std::cout << std::endl;
    }
}

bool CatWon(const Board& board, const Position& catPos) {
    return abs(catPos.x) == board.sideSize/2 || abs(catPos.y) == board.sideSize/2;
}

bool CatcherWon(const Board& board, const Position& catPos) {
    for(auto& neighbor : catPos.Neighbors())
        if(neighbor.IsInsideBoardBoundaries(board.sideSize) && !board.blocked[(neighbor.y + board.sideSize/2) * board.sideSize + neighbor.x + board.sideSize/2])
            return false;
    return true;
}

vector<Position> buildPath(Board& board){
    auto cat = board.catPos;

    vector<Position> path;
    queue<Position> frontier;
    unordered_set<Position> visited; // include the frontier too
    unordered_map<Position, Position> cameFrom;

    frontier.push(cat);
    visited.insert(cat);

    Position Exit = cat;

    while (frontier.size() > 0) {
        auto current = frontier.front();
        frontier.pop();

        if(CatWon(board, current)){
            Exit = current;
            break;
        }

        auto neighbors = board.NeighborsInsideBoundariesNotBlocked(current);
        for (auto& n : neighbors) {
            if (n != cat && !visited.contains(n)) {
                frontier.push(n);
                visited.insert(n);
                cameFrom[n] = current;
            }
        }
    }

    while (Exit != cat) {
        path.push_back(Exit);
        Exit = cameFrom[Exit];
    }

    return path;
}

#endif