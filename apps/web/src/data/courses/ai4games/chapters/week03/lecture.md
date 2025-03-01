# Game AI Engine

An AI engine can be understood as a system that manages and executes AI algorithms in a game. It acts as the brain of
the game, controlling various aspects of AI behavior, decision-making, and interactions with the game world.

The most common classes of AI engines commonly used in game development are:

- **Tree based**: Behavior Trees, Decision trees and State Machines;
- **Search-Based Systems**: A-star, Goal Oriented Action Planning, Hierarchical Task Networks, Hierarchical Task
  Networks, MinMax, Alpha-Beta Pruning, Monte Carlo Tree Search
- **Learning Based Systems**: Neural Networks, Reinforcement Learning, Genetic Algorithms, Q-Learning

``` mermaid
flowchart TD
    A["AI Engine"] --> Z["Agents"] & B["Classical Trees"] & C["Search"] & D["Learning"]
    C --> E["Dijkstra"]
    E --> F["A-Star"] & G["Heuristics"]
    G --> F & n3["MinMax"]
    D --> K["Neuron Networks"] & W["Reinforcement Learning"]
    W --> W2["Q-Learning"]
    B --> H["State Machines"] & I["Decision Tree"] & J["Behavior Tree"]
    F --> n1["Goal Oriented Action Planning"]
    n1 --> n2["Hierarchical Task Networks"]
    n3 --> n4["Alpha Beta Prunning"]
    n4 --> n5["Monte Carlo Tree Search"]
```

## Tree-Based Solvers

## Search-Based Solvers

## Learning-Based Solvers

