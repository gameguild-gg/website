# Wave Function Collapse (WFC) for Games

Ah, **Wave Function Collapse**—a term that sounds like it belongs in a science fiction novel, but in reality, it's an
algorithm with a fancy name that doesn’t really involve any quantum magic. It's widely used in **Procedural Content
Generation (PCG)**, a technique that game developers absolutely adore because it makes their lives easier. But don’t let
the quantum-themed name fool you—there’s no need for Schrödinger’s cat or any collapsing universes here.

::: note "🚨 **Personal Opinion Alert**:"
Who came up with this name? Sure, it sounds cool and science-y, but it's about as quantum as my morning coffee. Let’s be
real: they chose it because it’s more marketable. 😂
:::

In practice, WFC is just about solving constraint satisfaction problems. This magical-sounding technique was brought to
the limelight by [Maxim Gumin](https://github.com/mxgmn/WaveFunctionCollapse) in 2016. Maxim showed that WFC could
generate images and textures following specific rules, and game devs were like, “Yeah, we need that!”

## WFC in a Nutshell 🥜

Imagine you have a blank world (a grid, pixels, voxels, whatever). You also have a set of rules about what goes where (
e.g., no putting rivers next to mountains). WFC's job is to fill this world, slot by slot, making sure everything sticks
to the rules. It's a bit like Sudoku—but instead of numbers, you're dealing with game elements like terrain tiles,
rooms, or textures. Easy-peasy, right?

Here’s the general rundown:

* **Sampling the World**: First, create some slots to fill (think of pixels, textures, or rooms in a dungeon).
* **Set the Rules**: Define the constraints for your content—like which tile can sit next to which. No roads to nowhere!
* **Collapse the World**: Start with an empty world and let the algorithm fill in the blanks, one tile at a time,
  according to the rules. This is the "collapse" part.

Now let's break it down in detail so you can use this in your next game and look like a genius.

## Key Concepts of WFC 🤓

### Superposition (No, You’re Not in Physics Class)

At the start, each slot can be _anything_—just like your favorite RPG character before you completely ruin the build
with bad choices. 😅 In WFC, this means every tile can be in a "superposition" of many possible states. Over time, WFC
reduces this mess of possibilities by applying rules, eventually collapsing each slot to a single state.

### Adjacency Constraints: The “You Shall Not Pass” Rules 🚧

WFC’s bread and butter is adjacency constraints. You’ve got rules about which tiles can sit next to which. Picture a
road tile that can only connect to other roads, or a water tile that refuses to be friends with desert tiles. It’s all
about making sure the world makes sense.

If you’re making a dungeon, for example, you can say, "Room A must connect to hallways, but Room B only likes to hang
out with Room C and no one else." It’s like high school cliques, but for tiles.

### Entropy: The "Hot Mess" Meter 🔥

Remember entropy from physics class? It’s basically a measure of chaos. But in WFC, entropy just means the number of
options a tile has left. A tile with high entropy is still a hot mess—it could be _anything_. A tile with low entropy,
though, has its life together—it has only a few valid options. WFC works by collapsing the low-entropy tiles first, like
prioritizing the people who already know what they want for lunch. 🍔

### Backtracking: Undo Like a Pro ⏪

Sometimes, despite all the rules, WFC gets stuck. Maybe no valid tiles can go in a spot. This is when WFC says, “Oops,
my bad,” and _backtracks_ to try again. It's the algorithm’s way of saying, "No worries, let's undo that and fix it."

## Where Can You Use WFC in Games? 🎮

### 1\. Procedural Level Generation 🌍

One of the coolest things about WFC is its ability to generate levels that are not only random but also make sense. No
more roads to nowhere!

* **Dungeon Generation**: Imagine generating dungeons where rooms, hallways, and dead-ends all adhere to your design
  rules. WFC handles this beautifully.
* **Overworld Maps**: You can create sprawling open-world maps with biomes that transition logically—deserts next to
  dunes, mountains surrounded by hills. Say goodbye to floating islands next to volcanoes! 🌋

### 2\. Texture Synthesis 🎨

WFC’s original job was to generate textures from small samples. In game development, this means you can take a tiny bit
of grass or brick pattern and blow it up into a massive, seamless texture. You can forget those awkward texture seams
that make your world look like a patchwork quilt gone wrong.

### 3\. Tiling-Based Puzzle Games 🧩

WFC is also perfect for games where tile-matching mechanics reign supreme. You could generate puzzles or adjust game
boards dynamically, keeping things fresh and players on their toes.

### 4\. Story or Quest Generation 📜

WFC can go beyond just placing tiles—it can even help with generating quests or non-linear stories. Define a set of
events and rules about how they fit together, and let WFC create unique quest chains that stay coherent. Move over,
Dungeons & Dragons! 🐉

## How to Implement WFC in Games 🚀

### Step 1: Define Your Tile Set

First, decide what your tiles represent—whether they’re rooms, terrain, or objects. Next, define the constraints for
each tile. Room A connects to Hallways, Room B connects to Room C, and so on.

### Step 2: Set Up the Grid and Superposition

Set up a grid where each cell can be _any_ tile at first (superposition). WFC will gradually narrow down the
possibilities based on neighboring tiles.

### Step 3: Collapse Based on Entropy

Now, the algorithm will look at each tile and decide which ones can go where. It collapses the tiles with the least
options first (low entropy). Sort of like playing Tetris but with rules. 🎮

### Step 4: Propagate Constraints

As tiles are placed, WFC propagates the constraints to neighboring tiles, limiting their options. It keeps going until
the whole grid is filled.

### Step 5: Handle Backtracking

If WFC hits a dead end (it happens!), the algorithm backtracks, rewinds a bit, and tries again. Like Ctrl+Z but for
algorithms.

### Step 6: Optimize and Iterate

Once you've got the basics down, you can start optimizing. You might add symmetry rules to make the generated worlds
more aesthetically pleasing or speed up the algorithm with parallel processing. ⚙️

## The Pros and Cons of WFC 🎢

### Advantages

* **Consistency**: WFC creates coherent worlds, avoiding jarring transitions like forests sitting right next to cities.
  🌳🏙️
* **Versatility**: WFC can handle 2D and 3D worlds, quests, or even story generation.
* **Efficient Input**: You only need a small input set and a few rules to generate huge worlds.

### Challenges

* **Complexity**: Setting up those rules is no joke. Too many constraints, and your world becomes boring. Too few, and
  it’s chaos.
* **Performance**: WFC can be slow with large grids or lots of rules. Optimize or be prepared for some serious lag. 🐢
* **Overfitting**: Too many strict rules can lead to predictable or repetitive results. Mix it up for some randomness!

## Implementation for WFC 🛠️

``` c++
// Wave Function in C++
#include<queue>
#include <vector>
#include <unordered_map>
#include <set>
#include <stack>
#include <algorithm>
#include <random>

// Step 1: Define the tile set and adjacency constraints
// Change this based on your game's needs, you will need to link tiles to their their images or data
enum Tile { UNDEFINED, TileA, TileB, TileC, /* ... other tiles */ };

// set the rules for which tiles can be adjacent to each other
// unordered_map is O(1)  for lookups from the source tile
// unordered_set is O(1) for lookups from the target tile
std::unordered_map<Tile, std::unordered_set<Tile>> constraints = {
    { TileA, { TileB, TileC } }, // TileA can be adjacent to TileB and TileC
    { TileB, { TileA, TileC } },
    { TileC, { TileA } }
    // ... other tile constraints
};

// Define a cell structure to hold possible tiles and the final collapsed tile
struct Cell {
    std::set<Tile> possibleTiles; // Possible states (tiles) for this cell
    Tile collapsedTile = UNDEFINED; // Collapsed state when determined
};

// Step 2: Initialize the grid with superposition (all tiles are possible for each cell)
// vector of vectors to represent a 2D grid. modify this for 3D, anisotropic, graph or other grid types
std::vector<std::vector<Cell>> initializeGrid(int width, int height, const std::set<Tile>& tileSet) {
    std::vector<std::vector<Cell>> grid(width, std::vector<Cell>(height));
    for (auto& row : grid) {
        for (auto& cell : row) {
            cell.possibleTiles = tileSet; // Initially, every cell can be any tile
        }
    }
    return grid;
}

// Step 3: Calculate entropy based on the number of possible states per cell
// entropy is the number of possible states for a cell and it measures the uncertainty degree. The chaos temperature
int calculateEntropy(const Cell& cell) {
    return cell.possibleTiles.size();
}

// Step 4: Main function for Wave Function Collapse
void waveFunctionCollapse(std::vector<std::vector<Cell>>& grid) {
    std::stack<Cell*> backtrackStack;
    // you can use another random number generator or fine tune one to meet your needs
    std::default_random_engine generator;

    while (true) {
        // 4a: Find the cell with the lowest entropy
        // greedy approach. It does not guarantee the solution will always be found or consistent. To have that, you will have to implement a better constraint propagation. But you can backpropagate and try again if it becaomes stuck or inconsistent. 
        Cell* cell = findLowestEntropyCell(grid);
        if (!cell) break; // All cells are collapsed

        // 4b: Randomly select a tile from the possible options
        Tile chosenTile = randomChoice(cell->possibleTiles, generator);
        
        // 4c: Collapse the cell
        cell->collapsedTile = chosenTile;
        cell->possibleTiles = { chosenTile };
        
        // Save current state for potential backtracking
        backtrackStack.push(cell);
        
        // Step 5: Propagate constraints to update neighboring cells
        propagateConstraints(cell, grid, backtrackStack);

        // If no valid options remain, backtrack
        if (!backtrackStack.empty() && backtrackStack.top()->possibleTiles.empty()) {
            backtrack(grid, backtrackStack);
        }
    }
}

// Step 5: Propagation function to enforce adjacency constraints
void propagateConstraints(Cell* cell, std::vector<std::vector<Cell>>& grid, std::stack<Cell*>& backtrackStack) {
    for (Cell* neighbor : getNeighbors(cell, grid)) {
        // Remove tiles from neighbor's options that don't match adjacency rules
        for (auto it = neighbor->possibleTiles.begin(); it != neighbor->possibleTiles.end();) {
            if (constraints[cell->collapsedTile].find(*it) == constraints[cell->collapsedTile].end()) {
                it = neighbor->possibleTiles.erase(it);
            } else {
                ++it;
            }
        }
        
        // If neighbor has no valid options, prepare to backtrack
        if (neighbor->possibleTiles.empty()) {
            backtrackStack.push(neighbor);
        }
    }
}

// Step 6: Backtracking to fix conflicts
void backtrack(std::vector<std::vector<Cell>>& grid, std::stack<Cell*>& backtrackStack) {
    // Undo the last collapse step and reset the grid state
    while (!backtrackStack.empty()) {
        Cell* cell = backtrackStack.top();
        backtrackStack.pop();
        
        cell->possibleTiles.clear();
        cell->collapsedTile = Tile(); // Reset the collapsed state
    }
}

// Helper function to find the cell with the lowest entropy
Cell* findLowestEntropyCell(std::vector<std::vector<Cell>>& grid) {
    // you could rely on prioryty_queue or other data structures to optimize this
    // this is not optimized and will iterate over all cells
    Cell* minEntropyCell = nullptr;
    int minEntropy = INT_MAX;
    for (auto& row : grid) {
        for (auto& cell : row) {
            int entropy = calculateEntropy(cell);
            if (entropy > 1 && entropy < minEntropy) {
                minEntropyCell = &cell;
                minEntropy = entropy;
            }
        }
    }
    return minEntropyCell;
}

// Helper function to randomly select a tile from possible options
Tile randomChoice(const std::set<Tile>& tileOptions, std::default_random_engine& generator) {
    std::uniform_int_distribution<int> distribution(0, tileOptions.size() - 1);
    auto it = std::next(tileOptions.begin(), distribution(generator));
    return *it;
}

// Helper function to get neighbors of a cell (implementation depends on grid type and size)
std::vector<Cell*> getNeighbors(Cell* cell, std::vector<std::vector<Cell>>& grid) {
    std::vector<Cell*> neighbors;
    // Add neighbor finding logic here (e.g., adjacent cells in a 2D grid)
    return neighbors;
}
```

## Conclusion

Wave Function Collapse is the ultimate game dev hack. It helps generate complex, rule-following worlds without losing
the randomness that makes games exciting. Sure, it has its challenges—like any powerful tool—but once you get the hang
of it, WFC can make your worlds, quests, and even stories feel richer and more dynamic. Plus, it saves you a ton of
time!

So, next time you need a procedural level, a tile-based puzzle, or a randomly generated quest, let WFC do the heavy
lifting while you sit back and bask in the procedural glory.