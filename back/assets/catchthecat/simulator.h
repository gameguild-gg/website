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
#include <cstdint>
#include <ranges>

namespace Simulator {
    // forward declarations
    struct Board;
    struct Position;

    struct Position {
        // include cstdint
        int32_t x;
        int32_t y;

        Position(int32_t x, int32_t y) : x(x), y(y) {}

        Position() : x(0), y(0) {}

        Position(const Position &p) : x(p.x), y(p.y) {}

        Position(Position &&p) noexcept: x(p.x), y(p.y) {}

        Position(const std::pair<int32_t, int32_t> &p) : x(p.first), y(p.second) {}

        Position &operator=(const Position &p) = default;

        Position &operator=(const std::pair<int32_t, int32_t> &p) {
            x = p.first;
            y = p.second;
            return *this;
        }

        bool operator==(const Position &p) const {
            return x == p.x && y == p.y;
        }

        bool operator!=(const Position &p) const {
            return x != p.x || y != p.y;
        }

        inline Position NW() const {
            return (y % 2 == 0) ?
                   Position(x - 1, y - 1) :
                   Position(x, y - 1);
        }

        inline Position NE() const {
            return (y % 2 == 0) ?
                   Position(x, y - 1) :
                   Position(x + 1, y - 1);
        }

        inline Position E() const {
            return {x + 1, y};
        }

        inline Position W() const {
            return {x - 1, y};
        }

        inline Position SW() const {
            return (y % 2 == 0) ?
                   Position(x - 1, y + 1) :
                   Position(x, y + 1);
        }

        inline Position SE() const {
            return (y % 2 == 0) ?
                   Position(x, y + 1) :
                   Position(x + 1, y + 1);
        }

        inline bool IsInsideBoardBoundaries(int32_t sideSize) const {
            return abs(x) <= sideSize / 2 && abs(y) <= sideSize / 2;
        }

        inline std::vector<Position> Neighbors() const {
            return {NW(), NE(), E(), W(), SW(), SE()};
        }

        std::pair<int, int> toPair() { return {x, y}; }

        bool operator<(const Position &p) const {
            return x < p.x || (x == p.x && y < p.y);
        }

        uint64_t hash() const noexcept { return ((uint64_t) x) << 32 | (uint64_t) y; }
    }; // end of Position
}

namespace std {
    template<>
    struct hash<Simulator::Position> {
        std::size_t operator()(const Simulator::Position &p) const noexcept { return p.hash(); }
    };
}

namespace Simulator {
    void shuffleVector(std::vector<Position> &v) {
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

    struct AStarNode {
        Position pos;
        float accumulatedCost;
        float heuristic;

        // for priority_queue
        bool operator<(const AStarNode &n) const {
            // the operator should be > instead of < because the priority_queue is a max heap
            return this->accumulatedCost + this->heuristic > n.accumulatedCost + n.heuristic;
        }
    };

    enum class Turn {
        UNKNOWN,
        CAT,
        CATCHER
    };

    struct Board {
        std::vector<bool> blocked;
        int sideSize;
        Position catPos;
        Turn turn;

        Board(const Board &b) = default;

        Board(const std::vector<bool> &blocked, int sideSize, Position catPosition) : blocked(blocked), sideSize(sideSize), catPos(catPosition) {}

        inline bool isBlocked(const Position &p) const {
            // index = y*width + x;
            // given x and y have displacement, we have to add it to both
            // index = (y+width/2)*width + (x+width/2);
            return blocked[(p.y + sideSize / 2) * sideSize + (p.x + sideSize / 2)];
        }

        inline std::vector<Position> NeighborsInsideBoundaries(const Position &p) const {
            // filter Neighbors to only keep the ones inside the board boundaries
//            return p.Neighbors() | std::views::filter([this](const Position &neighbor) {
//                return neighbor.IsInsideBoardBoundaries(sideSize);
//            }); // c++23 with extensions enabled only

            std::vector<Position> neighbors = p.Neighbors();
            std::vector<Position> result;
            for (auto &neighbor: neighbors)
                if (neighbor.IsInsideBoardBoundaries(sideSize))
                    result.push_back(neighbor);
            shuffleVector(result);
            return result;
        }

        std::vector<Position> NeighborsInsideBoundariesNotBlocked(const Position &p) const {
            std::vector<Position> neighbors = NeighborsInsideBoundaries(p);
            std::vector<Position> result;
            for (auto &neighbor: neighbors)
                if (!isBlocked(neighbor))
                    result.push_back(neighbor);
            return result;
        }

        bool CatCanMoveToPosition(const Position &move) const {
            std::unordered_set<Position> possibleMoves;
            auto movableChoices = NeighborsInsideBoundariesNotBlocked(catPos);
            possibleMoves.insert(movableChoices.begin(), movableChoices.end());
            return possibleMoves.contains(move);
        }

        bool CatcherCanMoveToPosition(const Position &move) const {
            // inside boundaries, not blocked, and not cat
            return move.IsInsideBoardBoundaries(sideSize) &&
                   !isBlocked(move) &&
                   move != catPos;
        }

        bool CatWon() const {
            return abs(catPos.x) == sideSize / 2 || abs(catPos.y) == sideSize / 2;
        }

        bool CatWinAtPosition(const Position &pos) const {
            return abs(pos.x) == sideSize / 2 || abs(pos.y) == sideSize / 2;
        }

        bool CatcherWon() {
            auto neighbors = catPos.Neighbors();
            for (auto &neighbor: neighbors)
                if (neighbor.IsInsideBoardBoundaries(sideSize) && !isBlocked(neighbor))
                    return false;
            return true;
        }

        float distanceToBorder(Position p) const {
            // find the one that is greater in absolute value, X or Y.
            if (abs(p.y) > abs(p.x))
                return sideSize / 2 - abs(p.y);
            else
                return sideSize / 2 - abs(p.x);
        }
    };

    std::vector<bool> readBoard(int sideSize) {
        std::vector<bool> board;
        board.reserve(sideSize * sideSize);
        for (int i = 0; i < sideSize * sideSize; i++) {
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

    void printWithoutTime(const Board &b, const std::string &message) {
        std::cout << message << " " << b.sideSize << " " << b.catPos.x << " " << b.catPos.y << std::endl;
        auto catPos = b.catPos;
        catPos.x += b.sideSize / 2;
        catPos.y += b.sideSize / 2;
        auto catPosIndex = catPos.y * b.sideSize + catPos.x;
        for (int y = 0; y < b.sideSize; y++) {
            if (y % 2 == 1) std::cout << ' ';
            for (int x = 0; x < b.sideSize; x++) {
                if (y * b.sideSize + x == catPosIndex) {
                    std::cout << 'C';
                } else
                    std::cout << (b.blocked[y * b.sideSize + x] ? '#' : '.');
                if (x < b.sideSize - 1) std::cout << ' ';
            }
            std::cout << std::endl;
        }
    }

    void printWithTime(const Board &b, const std::string &message, long long time) {
        printWithoutTime(b, message);
        std::cout << time << std::endl;
    }

// Build Path Algorithm for A*
    std::vector<Position> buildPath(Board &board) {
        std::vector<Position> path; // to be built and returned
        std::priority_queue<AStarNode> frontier; // the elements that will be explored
        std::unordered_map<Position, Position> cameFrom; // the flow field. if have the key, it means it is visited

        Position exitIt = board.catPos; // the exit position when we found it
        frontier.push({board.catPos, 0, board.distanceToBorder(board.catPos)}); // bootstrap
        cameFrom[board.catPos] = board.catPos; // bootstrap
        while (frontier.size() > 0) { // while we can consume
            auto current = frontier.top(); // get the next element to consume
            frontier.pop(); // consume
            if (board.CatWinAtPosition(current.pos)) { // stop condition
                exitIt = current.pos; // store the exit position
                break; // quit early
            }
            auto candidates = board.NeighborsInsideBoundariesNotBlocked(current.pos); // vicinity
            for (const auto &neighbor: candidates) // iterate over the candidates
                if (neighbor != board.catPos && !cameFrom.contains(neighbor)) { // if not visited and not the cat
                    frontier.push({neighbor, current.accumulatedCost + 1, board.distanceToBorder(neighbor)}); // produce
                    cameFrom[neighbor] = current.pos; // update flow field
                }
        }
        while (exitIt != board.catPos) { // build the path. using Exit as iterator
            path.push_back(exitIt); // add to the path
            exitIt = cameFrom[exitIt]; // update the iterator
        } // path.front() is the exit, path.back() is first possible cat move
        return path; // return the path
    }

    Turn getTurn(const std::string &turn) {
        if (turn == "CAT")
            return Turn::CAT;
        else if (turn == "CATCHER")
            return Turn::CATCHER;
        else
            return Turn::UNKNOWN;
    }
}
#endif