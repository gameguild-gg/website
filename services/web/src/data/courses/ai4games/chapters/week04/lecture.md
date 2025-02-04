# Goal Oriented Action Planning

## 1. A* Recap and Producer-Consumer Priority Strategy

::: note "Review"
For more details on A* check the previous lecture.
:::

### 1.1. A* Recap

A* is a best-first search algorithm that uses a cost function f(n) = g(n) + h(n) where:

- **g(n)**: The cost from the start node to node n.
- **h(n)**: The heuristic estimate from node n to the goal.

A* maintains two key sets:

- **Frontier (Open Set)**: A priority queue (producer-consumer style) of nodes to explore next. The node with the smallest f value is “consumed” next.
- **Visited (Closed Set)**: A set (hashtable) of nodes that have been evaluated, to avoid reprocessing.

The producer-consumer priority queue pattern works as follows:

- **Producer**: When expanding a node, you "produce" its neighbors by calculating their tentative `g` costs and pushing them into the priority queue.
- **Consumer**: The algorithm "consumes" (i.e., pops) the node with the lowest `f` value from the priority queue, processing it as the next candidate for path extension.

This pattern allows A* to efficiently home in on the optimal path.

---

## 2. Introduction to Goal-Oriented Action Planning (GOAP)

### 2.1. What is GOAP?

Goal-Oriented Action Planning (GOAP) is an AI decision-making strategy where an agent plans a sequence of actions to achieve one or more goals. Rather than merely moving through a grid (like in A*), GOAP's planning engine considers:

::: warning "GOAP Components"
There are a few key components in GOAP to be aware of, but most of them are similar to A*. We will deep dive into them later.
:::

- **Actions**: What the agent can do.
    - **Preconditions**: What conditions must be true for an action to be executed.
    - **Effects**: How the world (or agent’s state) changes after an action.
    - **Cost**: How "expensive" an action is to execute.
- **Goals**: What the agent wants to achieve.
    - **Conditions**: What must be true for a goal to be considered achieved.
    - **Priority**: How important the goal is relative to others, useful when you implement multiple goals or orchestration.
- **State**: The current state of the world and the agent.
    - **Key-Value Pairs**: Representing various conditions (e.g., health, ammo, enemy presence).
    - **State Transitions**: How the state changes as actions are executed
- **Planning**:
    - **Search Algorithm**: Similar to A*, but with a focus on action sequences.
    - **Frontier** and **Visited**: Managing the search space of possible action sequences.
    - **Heuristic**: Estimating the cost from the current state to the goal.

The planner searches through the space of actions (and resulting states) to find a sequence that satisfies the goal conditions. 

The planning problem (or solver) is similar to graph search (with nodes representing states and edges representing actions), many of the ideas from A* (like heuristics and frontier management) can be re-used.

### 2.2. Why Transform A* into GOAP?

By transforming A* into GOAP, you create a unified planning system where:

- **Pathfinding** is a **Special Case**: If the only action is "move" and the state includes position, then planning a path is equivalent to finding a route on a grid.
- **Generalized Planning**: The same system can plan complex behaviors (like "attack enemy" or "collect resource") by considering multiple actions and their interdependencies.

---

## 3. Designing a GOAP Solver in C++

### 3.1. Data Structures

The planning engine will use a search algorithm (similar to A*) where:

- Nodes: Represent states (including the accumulated effects of previous actions).
- Edges: Represent actions that transition from one state to the next.
- Heuristic: Estimates the cost from a given state to the goal.

### 3.2. C++ Implementation Overview

Below are simplified C++ classes to illustrate how you might begin evolving an A* algorithm into a GOAP solver.

#### 3.2.1. State Representation

We can represent the state as a mapping of keys (strings) to values (booleans, ints, etc.). For simplicity, here we use a `std::unordered_map`.

``` cpp
#include <string>
#include <unordered_map>

using State = std::unordered_map<std::string, int>;

// Utility function to check if a state satisfies a condition.
bool satisfies(const State& current, const State& conditions) {
    for (const auto& [key, value] : conditions) {
        auto it = current.find(key);
        if (it == current.end() || it->second != value)
            return false;
    }
    return true;
}
```

!!! quiz
{
"title": "State Representation",
"question": "In GOAP, how is a state typically represented?",
"options": ["A list of booleans", "A set of conditions without values", "Key-value pairs representing conditions and their values", "A single integer value"],
"answers": ["Key-value pairs representing conditions and their values"]
}
!!!

::: note "Variant"
Optionally, you could also use a `std::variant` from `C++17' to represent different types of values in the state instead of just `int`.
``` c++
#include <variant>
#include <iostream>
#include <string>

int main() {
std::variant<int, double, std::string> var;
    // Assign different types to the variant
    var = 42;                        // Holds int
    std::cout << std::get<int>(var) << std::endl;

    var = 3.14;                      // Holds double
    std::cout << std::get<double>(var) << std::endl;

    var = "Hello, world!";           // Holds string
    std::cout << std::get<std::string>(var) << std::endl;

    return 0;
}
```

You can test the type before geting the value like:
``` cpp
if (std::holds_alternative<int>(var)) {
    std::cout << "Variant holds an int: " << std::get<int>(var) << std::endl;
}
```
:::


#### 3.2.2. Action Class

An abstract base class for actions with preconditions, effects, and cost:

``` cpp
#include <memory>
#include <vector>

class Action {
public:
    virtual ~Action() {}

    // Preconditions that must be satisfied for the action to be applicable.
    virtual State getPreconditions() const = 0;

    // Effects to be applied after the action is executed.
    virtual State getEffects() const = 0;

    // Cost of executing this action.
    virtual float getCost() const = 0;

    // A unique name/identifier for debugging purposes.
    virtual std::string getName() const = 0;

    // Check if the action is applicable in the current state.
    bool isApplicable(const State& state) const {
        return satisfies(state, getPreconditions());
    }
};
```

::: note "Resiliency"
It might be interesting to pass some context data to the action so it would be reasoning better about the effects.
:::

!!! quiz
{
"title": "Action Preconditions",
"question": "Which of the following best describes preconditions for actions in GOAP?",
"options": ["The visual effects of an action", "Requirements that must be true for an action to be executed", "The cost value of an action", "A random state generated by the system"],
"answers": ["Requirements that must be true for an action to be executed"]
}
!!!

#### 3.2.3. Derived Action Classes: Moving and Attacking

Now, let’s implement two simple derived actions.

::: tip "Moving Action"
For the pathfinding example, imagine that the state has keys like `x` and `y` for position. The Move action would have preconditions that the agent is at a certain location and effects that update the position.
:::

``` cpp
#include <sstream>

class MoveAction : public Action {
private:
    int targetX;
    int targetY;
    float cost;

public:
    MoveAction(int tx, int ty, float cost = 1.0f)
        : targetX(tx), targetY(ty), cost(cost) {}

    State getPreconditions() const override {
        // For a move, you might have a precondition such as "isAdjacent" (or simply, the state must have a valid position)
        // Here we leave it empty to denote that move is always applicable if not obstructed.
        return State();
    }

    State getEffects() const override {
        // The effect is to set the new position.
        return State{{"x", targetX}, {"y", targetY}};
    }

    float getCost() const override {
        return cost;
    }

    std::string getName() const override {
        std::ostringstream oss;
        oss << "Move to (" << targetX << ", " << targetY << ")";
        return oss.str();
    }
};
```

::: tip "Attacking Action"
The Attack action might have preconditions like the target being within range and effects that change the health of the target.
:::

``` cpp
class AttackAction : public Action {
private:
    std::string target;
    float cost;
public:
    AttackAction(const std::string& target, float cost = 2.0f)
        : target(target), cost(cost) {}

    State getPreconditions() const override {
        // Example: Must have an enemy in range.
        return State{{"enemyInRange", 1}};
    }

    State getEffects() const override {
        // Example: Effect might reduce enemy health. Here, "enemyHealth" is decreased.
        return State{{"enemyHealth", -10}}; // This indicates a change; in a full implementation you'd handle deltas.
    }

    float getCost() const override {
        return cost;
    }

    std::string getName() const override {
        return "Attack " + target;
    }
};
```

---

## 4. Evolving A* into GOAP

### 4.1. From Pathfinding to Action Planning

In A*, nodes are grid positions, and edges are moves between positions. In GOAP:

- Nodes: Represent world states (which may include position, health, enemy status, etc.).
- Edges: Represent actions (like Move or Attack) that transition one state to another.
- Cost: Each action’s cost contributes to the total cost, similar to how moving from one node to another adds a cost in A*.
- Frontier and Visited: The same idea applies. We can maintain a priority queue (frontier) of partial plans (each associated with a state and accumulated cost) and a visited set to avoid cycles or redundant work.

!!! quiz
{
"title": "Action Cost",
"question": "Why is action cost important in GOAP?",
"options": ["To make actions more complex", "To avoid conflicts between actions", "To prioritize cheaper sequences of actions when planning", "To enforce random action selections"],
"answers": ["To prioritize cheaper sequences of actions when planning"]
}
!!!

### 4.2. GOAP Solver Outline

Here is a pseudocode/outline in C++ that shows how you might set up the planning loop:

``` cpp
#include <queue>
#include <set>
#include <functional>

// A node in the planning search graph.
struct PlanNode {
    State state;
    float costSoFar;
    float estimatedTotalCost; // costSoFar + heuristic(state)
    std::vector<std::shared_ptr<Action>> actions; // sequence of actions that led here

    // For use in the priority queue.
    bool operator>(const PlanNode& other) const {
        return estimatedTotalCost > other.estimatedTotalCost;
    }
};

// Heuristic function to estimate remaining cost from a state to goal.
// For a pathfinder, this might be Manhattan distance; for other goals, design accordingly.
float heuristic(const State& current, const State& goal) {
    // This is a placeholder, you should implement a heuristic relevant to your state and resilent to not only position.
    // For example, if state includes x and y, you can compute Manhattan distance:
    int dx = abs(current.at("x") - goal.at("x"));
    int dy = abs(current.at("y") - goal.at("y"));
    return static_cast<float>(dx + dy);
}

std::vector<std::shared_ptr<Action>> planGOAP(const State& start, const State& goal,
                                              const std::vector<std::shared_ptr<Action>>& availableActions) {
    std::priority_queue<PlanNode, std::vector<PlanNode>, std::greater<PlanNode>> frontier;
    std::set<State> visited; // You might need a custom comparator for State.

    // Start node.
    PlanNode startNode{start, 0.0f, heuristic(start, goal), {}};
    frontier.push(startNode);

    while (!frontier.empty()) {
        PlanNode current = frontier.top();
        frontier.pop();

        // Check if current state satisfies goal conditions.
        if (satisfies(current.state, goal)) {
            return current.actions;
        }

        // Mark state as visited (note: for complex states, use a proper hash/comparison).
        // visited.insert(current.state); // Pseudocode: ensure State is hashable.

        // Expand each available action.
        for (auto& action : availableActions) {
            if (action->isApplicable(current.state)) {
                // Compute new state by merging current state with the action’s effects.
                State newState = current.state;
                State effects = action->getEffects();
                for (const auto& [key, value] : effects) {
                    // In a full implementation, handle effects as deltas or absolute settings.
                    newState[key] = value;
                }
                float newCost = current.costSoFar + action->getCost();
                float estimated = newCost + heuristic(newState, goal);

                // Create new plan node.
                PlanNode next{newState, newCost, estimated, current.actions};
                next.actions.push_back(action);

                // Check if we’ve already visited this state.
                // if (visited.find(newState) == visited.end())
                {
                    frontier.push(next);
                }
            }
        }
    }
    // No plan found.
    return {};
}
```

::: note

In a full implementation you will need to implement:

- A proper hash and equality function for the State type.
- A mechanism to correctly merge effects (e.g., applying deltas versus overwriting values).
- Handling of more complex action preconditions/effects.
:::

!!! quiz
{
"title": "Planner's Role",
"question": "What is the primary role of the GOAP planner?",
"options": ["Randomly select actions for agents", "Optimize heuristic functions only", "Find an optimal sequence of actions to achieve a goal", "Control the physics of the game environment"],
"answers": ["Find an optimal sequence of actions to achieve a goal"]
}
!!!

### 4.3. Specializing as a Path Finder

If you set up your MoveAction so that:

- Its preconditions verify that the target is a neighbor of the current position.
- Its effects update the "x" and "y" values in the state.

Then planning a series of MoveActions from the start to the goal becomes equivalent to A* pathfinding on a grid. You’ve simply “generalized” the process to support additional actions (like AttackAction) by extending the same planning logic.

``` cpp
#include <algorithm>
#include <cmath>
#include <iostream>
#include <queue>
#include <set>
#include <sstream>
#include <string>
#include <unordered_map>
#include <unordered_set>
#include <vector>
#include <memory>

// Using State as a collection of key-value pairs.
using State = std::unordered_map<std::string, int>;

//
// Utility function to check if a state satisfies a condition.
//
bool satisfies(const State &current, const State &conditions) {
    for (const auto &[key, value] : conditions) {
        auto it = current.find(key);
        if (it == current.end() || it->second != value)
            return false;
    }
    return true;
}

//
// Returns a canonical string representation of a State.
// This is used for the visited set to ensure we don't revisit states.
// This function sorts the keys so that the ordering is consistent.
//
std::string stateToString(const State &state) {
    std::vector<std::string> entries;
    for (const auto &kv : state) {
        entries.push_back(kv.first + ":" + std::to_string(kv.second));
    }
    std::sort(entries.begin(), entries.end());
    std::string result;
    for (const auto &entry : entries) {
        result += entry + ";";
    }
    return result;
}

//
// Base class for GOAP actions.
// Actions define preconditions, effects, cost, and a name.
// An overridable method applyEffects() is provided to allow more sophisticated
// merging of effects into the current state.
//
class Action {
public:
    virtual ~Action() {}

    // Returns the preconditions that must be satisfied for this action.
    virtual State getPreconditions() const = 0;

    // Returns the effects of the action.
    virtual State getEffects() const = 0;

    // Returns the cost of performing this action.
    virtual float getCost() const = 0;

    // Returns a human-readable name/identifier for debugging.
    virtual std::string getName() const = 0;

    // Check if the action is applicable given the current state.
    bool isApplicable(const State &state) const {
        return satisfies(state, getPreconditions());
    }

    // Apply the effects of the action on a given state.
    // By default, this implementation simply overwrites or adds key-value pairs.
    // For more complex behavior (e.g., applying delta changes), override this method.
    virtual void applyEffects(State &state) const {
        State effects = getEffects();
        for (const auto &[key, value] : effects) {
            state[key] = value;
        }
    }
};

//
// A sample MoveAction that can be used for pathfinding.
// This action changes the agent's position (represented by keys "x" and "y").
//
class MoveAction : public Action {
private:
    int targetX;
    int targetY;
    float cost;

public:
    MoveAction(int tx, int ty, float cost = 1.0f)
        : targetX(tx), targetY(ty), cost(cost) {}

    State getPreconditions() const override {
        // For a move, we might check that the target cell is adjacent
        // to the current position. For simplicity, we assume the move is always possible.
        return State();
    }

    State getEffects() const override {
        // The effect is to set the new position.
        return State{{"x", targetX}, {"y", targetY}};
    }

    float getCost() const override {
        return cost;
    }

    std::string getName() const override {
        std::ostringstream oss;
        oss << "Move to (" << targetX << ", " << targetY << ")";
        return oss.str();
    }
};

//
// A sample AttackAction that can be used in a combat scenario.
//
class AttackAction : public Action {
private:
    std::string target;
    float cost;
public:
    AttackAction(const std::string &target, float cost = 2.0f)
        : target(target), cost(cost) {}

    State getPreconditions() const override {
        // Example: Ensure an enemy is in range.
        return State{{"enemyInRange", 1}};
    }

    State getEffects() const override {
        // Example: Decrease enemy health.
        // In a full implementation, you might handle this as a delta.
        return State{{"enemyHealth", -10}};
    }

    float getCost() const override {
        return cost;
    }

    std::string getName() const override {
        return "Attack " + target;
    }
};

//
// Node structure used by the planning algorithm.
// Each node represents a state reached by executing a sequence of actions,
// along with the cumulative cost and estimated total cost.
//
struct PlanNode {
    State state;
    float costSoFar;
    float estimatedTotalCost; // costSoFar + heuristic(state)
    std::vector<std::shared_ptr<Action>> actions; // Sequence of actions that led here.

    // For use in the priority queue.
    bool operator>(const PlanNode &other) const {
        return estimatedTotalCost > other.estimatedTotalCost;
    }
};

//
// Heuristic function to estimate the remaining cost from the current state to the goal.
// For a pathfinder, this might be Manhattan distance based on "x" and "y" values.
// For a more general GOAP use, modify this function to account for multiple factors.
//
float heuristic(const State &current, const State &goal) {
    // Check if the keys exist in both states.
    if (current.find("x") != current.end() && current.find("y") != current.end() &&
        goal.find("x") != goal.end() && goal.find("y") != goal.end()) {
        int dx = std::abs(current.at("x") - goal.at("x"));
        int dy = std::abs(current.at("y") - goal.at("y"));
        return static_cast<float>(dx + dy);
    }
    // Fallback heuristic if position is not part of the state.
    return 0.0f;
}

//
// The planning function that implements a GOAP search algorithm using a producer-consumer pattern.
// The same function can be used as a pathfinder when the only actions are MoveActions.
//
std::vector<std::shared_ptr<Action>> planGOAP(
    const State &start,
    const State &goal,
    const std::vector<std::shared_ptr<Action>> &availableActions) {

    // Priority queue for nodes to be expanded.
    std::priority_queue<PlanNode, std::vector<PlanNode>, std::greater<PlanNode>> frontier;

    // Use an unordered_set of state strings to track visited states.
    std::unordered_set<std::string> visited;

    // Initialize the start node.
    PlanNode startNode{start, 0.0f, heuristic(start, goal), {}};
    frontier.push(startNode);

    while (!frontier.empty()) {
        PlanNode current = frontier.top();
        frontier.pop();

        // If current state satisfies the goal conditions, return the action sequence.
        if (satisfies(current.state, goal)) {
            return current.actions;
        }

        // Generate a unique key for the current state.
        std::string currentKey = stateToString(current.state);
        if (visited.find(currentKey) != visited.end()) {
            continue; // Skip already visited states.
        }
        visited.insert(currentKey);

        // Expand each available action.
        for (const auto &action : availableActions) {
            if (action->isApplicable(current.state)) {
                // Compute new state by applying the action's effects.
                State newState = current.state;
                action->applyEffects(newState);

                float newCost = current.costSoFar + action->getCost();
                float estimated = newCost + heuristic(newState, goal);

                // Create a new plan node.
                PlanNode next{newState, newCost, estimated, current.actions};
                next.actions.push_back(action);

                // Only push if this state hasn't been visited.
                std::string newStateKey = stateToString(newState);
                if (visited.find(newStateKey) == visited.end()) {
                    frontier.push(next);
                }
            }
        }
    }
    // If no plan is found, return an empty sequence.
    return {};
}

//
// Example usage (for testing purposes).
// Uncomment and adjust as needed.
//
int main() {
    State start{{"x", 0}, {"y", 0}, {"enemyInRange", 0}};
    State goal{{"x", 2}, {"y", 2}};
    
    std::vector<std::shared_ptr<Action>> actions;
    actions.push_back(std::make_shared<MoveAction>(1, 0));
    actions.push_back(std::make_shared<MoveAction>(1, 1));
    actions.push_back(std::make_shared<MoveAction>(2, 2));
    actions.push_back(std::make_shared<AttackAction>("Goblin"));
    
    auto plan = planGOAP(start, goal, actions);
    for (const auto &action : plan) {
        std::cout << action->getName() << std::endl;
    }
    
    return 0;
}
```