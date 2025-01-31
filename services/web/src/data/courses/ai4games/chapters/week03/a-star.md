# Pathfinding on a 2D grid

## Data structures

In order to build an A-star pathfinding algorithm, we need to define some data structures. We need:

- Index for the quantized map;
- Position for the game objects;
- Bucket to query in O(1) if the elements are there;
- Map from Index to Buckets;
- Priority Queue to store the frontier of visitable buckets;
- Vector of Indexes to store the path;

## Index and Position

In order to A-star to work in a continuous space, we should quantize the space position into indexes.

``` c++
// generic vector2 struct to work with floats and ints
template <typename T>
// requires T to be int32_t or float_t
requires std::is_same<T, int32_t>::value || std::is_same<T, float_t>::value // C++20
struct Vector2 {
    // data
    T x, y;
    // constructors
    Vector2() : x(0), y(0) {}
    Vector2(T x, T y) : x(x), y(y) {}
    // copy constructor
    Vector2(const Vector2& v) : x(v.x), y(v.y) {}
    // assignment operator
    Vector2& operator=(const Vector2& v) {
        x = v.x;
        y = v.y;
        return *this;
    }
    // operators
    Vector2 operator+(const Vector2& v) const {
        return Vector2(x + v.x, y + v.y);
    }
    Vector2 operator-(const Vector2& v) const {
        return Vector2(x - v.x, y - v.y);
    }
    // distance
    float distance(const Vector2& v) const {
        return sqrt((x - v.x) * (x - v.x) + (y - v.y) * (y - v.y));
    }
    // distance squared
    float distanceSquared(const Vector2& v) const {
        return (x - v.x) * (x - v.x) + (y - v.y) * (y - v.y);
    }
    // quantize to index2
    Vector2<int32_t> quantized(float scale=1) const {
        return {(int32_t)std::round(x / scale), (int32_t)std::round(y / scale)};
    }
    // operator < for std::map
    bool operator<(const Vector2& v) const {
        return x < v.x || (x == v.x && y < v.y);
    }
    // operator == for std::map
    bool operator==(const Vector2& v) const {
        return x == v.x && y == v.y;
    }
};
```

- The operators `<` and `==` are required to use the Vector2 as a key in a std::map.
- The `quantized` method is used to convert a position into an index.
- The `distance` and `distanceSquared` methods are used to calculate the distance between two positions. Is used on A-star to calculate the cost to reach a neighbor or the distance to the goal.

```c++
using Index2 = Vector2<int32_t>;
using Position2 = Vector2<float_t>;
```

I am going to use `Index2` to store the quantized index in the grid and `Position2` to store the continuous position.

```c++
// hash function for std::unordered_map
template <>
struct std::hash<Index2> {
    size_t operator()(const Index2 &v) const {
        return (((size_t)v.x) << 32) ^ (size_t)v.y;
    }
};
```

This hash function is for the `std::unordered_map` and `std::unordered_set` to work with `Index2`.

## Bucket

In order to have an easy way to query if a game object is in a bucket, we need to use an `std::unordered_set` of pointers to the game objects. In order to index them, we will use an `std::unordered_map` from `Index2` to `std::unordered_set`.

```c++
std::unordered_map<Index2, std::unordered_set<GameObject*>> quantizedMap;
```

## Costs

Your scenario might have different costs to reach a bucket. You can use an `std::unordered_map` to store the cost of each bucket.

```c++
std::unordered_map<Index2, float> costMap;
```

## Walls

You might want to avoid some buckets. You can use an `std::unordered_map` to store the walls.

```c++
std::unordered_map<Index2, bool> isWall;
```

## Priority Queue

In order to store the frontier of visitable buckets, we need to use a `std::priority_queue` of pairs of `float` and `Index2`.

```c++
std::priority_queue<std::pair<float, Index2>> frontier;
```

## Implementation

``` c++
/**
In order to build an A-star pathfinding algorithm, we need to define some data structures. We need:
- Index for the quantized map;
- Position2 for the game objects;
- Bucket to query in O(1) if the elements are there;
- Map from Index to Buckets;
- Priority Queue to store the frontier of visitable buckets;
- Vector of Indexes to store the path;
*/

#include <iostream>
#include <unordered_map>
#include <unordered_set>
#include <cmath>
#include <vector>
#include <queue>

using std::pair;

template<typename K, typename V>
using umap = std::unordered_map<K, V>;

template<typename T>
using uset = std::unordered_set<T>;

template<typename T>
using pqueue = std::priority_queue<T>;

// generic vector2 struct to work with floats and ints
template <typename T>
// requires T to be int32_t or float_t
requires std::is_same<T, int32_t>::value || std::is_same<T, float_t>::value // C++20
struct Vector2 {
    // data
    T x, y;
    // constructors
    Vector2() : x(0), y(0) {}
    Vector2(T x, T y) : x(x), y(y) {}
    // copy constructor
    Vector2(const Vector2& v) : x(v.x), y(v.y) {}
    // assignment operator
    Vector2& operator=(const Vector2& v) {
        x = v.x;
        y = v.y;
        return *this;
    }
    // operators
    Vector2 operator+(const Vector2& v) const {
        return Vector2(x + v.x, y + v.y);
    }
    Vector2 operator-(const Vector2& v) const {
        return Vector2(x - v.x, y - v.y);
    }
    // distance
    float distance(const Vector2& v) const {
        return sqrt((x - v.x) * (x - v.x) + (y - v.y) * (y - v.y));
    }
    // distance squared
    float distanceSquared(const Vector2& v) const {
        return (float)(x - v.x) * (x - v.x) + (float)(y - v.y) * (y - v.y);
    }
    // quantize to index2
    Vector2<int32_t> quantized(float scale=1) const {
        return {(int32_t)std::round(x / scale), (int32_t)std::round(y / scale)};
    }
    // operator < for std::map
    bool operator<(const Vector2& v) const {
        return x < v.x || (x == v.x && y < v.y);
    }
    // operator == for std::map
    bool operator==(const Vector2& v) const {
        return x == v.x && y == v.y;
    }
};

using Index2 = Vector2<int32_t>;
using Position2 = Vector2<float_t>;

// implement this struct to store game objects by yourself
struct GameObject {
    Position2 position;
    // add here your other data

    GameObject(const Position2& position) : position(position) {}
    GameObject() : position(Position2()) {}
};

// hash function for std::unordered_map
template <>
struct std::hash<Index2> {
    size_t operator()(const Index2 &v) const {
        return (((size_t)v.x) << 32) | (size_t)v.y;
    }
};

// The game objects organized into buckets
umap<Index2, uset<GameObject*>> quantizedMap;
// all game objects
uset<GameObject*> gameObjects;
// The cost of each bucket
umap<Index2, float> costMap;
// The walls
umap<Index2, bool> isWall;

// Pathfinding algorithm from position A to position B
std::vector<Index2> findPath(const Position2& startPos, const Position2& endPos) {
    // quantize
    Index2 start = startPos.quantized();
    Index2 end = endPos.quantized();

    // datastructures
    pqueue<pair<float, Index2>> frontier; // to store the frontier of visitable buckets
    umap<Index2, float> accumulatedCosts; // to store the cost to reach a bucket

    // initialize
    accumulatedCosts[start] = 0;
    frontier.emplace(0, start);

    // main loop
    while (!frontier.empty()) {
        // consume first element from the frontier
        auto current = frontier.top().second;
        frontier.pop();

        // quit early
        if (current == end)
            break;

        // iterate over neighbors
        auto candidates = {
                current + Index2(1, 0),
                current + Index2(-1, 0),
                current + Index2(0, 1),
                current + Index2(0, -1)
        };
        for (const auto& next : candidates) {
            // skip walls
            if(isWall.contains(current))
                continue;
            // if the neighbor has not been visited and is not on frontier
            // calculate the cost to reach the neighbor
            float newCost =
                    accumulatedCosts[current] + // cost so far
                    current.distance(next) + // cost to reach the neighbor
                    (costMap.contains(next) ? costMap[next] : 0); // cost of the neighbor
            // if the cost is lower than the previous cost
            if (!accumulatedCosts.contains(next) || newCost < accumulatedCosts[next]) {
                // update the cost
                accumulatedCosts[next] = newCost;
                // calculate the priority
                float priority = newCost + next.distance(end);
                // push the neighbor to the frontier
                frontier.emplace(-priority, next);
            }
        }
    }

    // reconstruct path
    std::vector<Index2> path;
    Index2 current = end;
    while (current != start) {
        path.push_back(current);
        auto candidates = {
                current + Index2(1, 0),
                current + Index2(-1, 0),
                current + Index2(0, 1),
                current + Index2(0, -1)
        };
        for (const auto& next : candidates) {
            if (accumulatedCosts.contains(next) && accumulatedCosts[next] < accumulatedCosts[current]) {
                current = next;
                break;
            }
        }
    }
    path.push_back(start);
    std::reverse(path.begin(), path.end());
    return path;
}

int main() {
/*
map. numbers are bucket cost, letters are objects, x is wall
A 0 5 0 0 0
0 X X 0 0 0
5 X 0 0 5 0
0 0 0 5 B 5
0 0 0 0 5 0
 */

    // Create 2 Game Objects
    GameObject a(Position2(0.1, 0.1));
    GameObject b(Position2(3.9, 4.1));

    // place walls
    isWall[Index2(1, 1)] = true;
    isWall[Index2(1, 2)] = true;
    isWall[Index2(2, 1)] = true;

    // add cost to some buckets
    // should avoid these:
    costMap[Index2(2, 0)] = 5;
    costMap[Index2(0, 2)] = 5;
    // should pass-through these:
    costMap[Index2(5, 4)] = 5;
    costMap[Index2(3, 4)] = 5;
    costMap[Index2(4, 3)] = 5;
    costMap[Index2(4, 5)] = 5;

    // add game objects to the set
    gameObjects.insert(&a);
    gameObjects.insert(&b);

    // add game objects to the quantized map
    for (auto& g : gameObjects)
        quantizedMap[g->position.quantized()].insert(g);

    // find path
    auto path = findPath(a.position, b.position);

    // todo: smooth the path between the points

    // print path
    for (auto& p : path)
        std::cout << "(" << p.x << ", " << p.y << ") ";
    std::cout << std::endl;
    // will print (0, 0) (1, 0) (1, -1) (2, -1) (3, -1) (3, 0) (3, 1) (3, 2) (3, 3) (4, 3) (4, 4)

    return 0;
}
```
