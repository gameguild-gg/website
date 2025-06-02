# Making the GOAP planner Dynamic

``` c++
#include <algorithm>
#include <cmath>
#include <functional>
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
// The keys are sorted to provide a consistent ordering.
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
    // By default, simply overwrites or adds key-value pairs.
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
        // For a move, you might check that the target cell is valid.
        // Here we assume that the move is always allowed.
        return State();
    }

    State getEffects() const override {
        // The effect is to update the position.
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
// A sample AttackAction for a combat scenario.
//
class AttackAction : public Action {
private:
    std::string target;
    float cost;
public:
    AttackAction(const std::string &target, float cost = 2.0f)
        : target(target), cost(cost) {}

    State getPreconditions() const override {
        // For example, ensure an enemy is in range.
        return State{{"enemyInRange", 1}};
    }

    State getEffects() const override {
        // Example: decrease enemy health (could be implemented as a delta in a more complex system).
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
// along with the cumulative cost and estimated total cost (costSoFar + heuristic).
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
// For a pathfinder, we use Manhattan distance based on "x" and "y" values.
// For a more general GOAP use, modify this function as needed.
//
float heuristic(const State &current, const State &goal) {
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
// This version of the planner takes a dynamic action generator function.
// Rather than passing in a fixed list of actions, you supply a function that,
// given the current state, returns the available actions.
// This makes the planner suitable for tasks like pathfinding where actions (neighbors)
// are generated on the fly.
//
std::vector<std::shared_ptr<Action>> planGOAPDynamic(
    const State &start,
    const State &goal,
    std::function<std::vector<std::shared_ptr<Action>>(const State&)> generateActions) {

    // Priority queue for nodes to be expanded.
    std::priority_queue<PlanNode, std::vector<PlanNode>, std::greater<PlanNode>> frontier;

    // Visited states (using string representation to avoid duplicate states).
    std::unordered_set<std::string> visited;

    // Initialize the start node.
    PlanNode startNode{start, 0.0f, heuristic(start, goal), {}};
    frontier.push(startNode);

    while (!frontier.empty()) {
        PlanNode current = frontier.top();
        frontier.pop();

        // Check if the current state satisfies the goal.
        if (satisfies(current.state, goal)) {
            return current.actions;
        }

        // Skip states that have been seen already.
        std::string currentKey = stateToString(current.state);
        if (visited.find(currentKey) != visited.end()) {
            continue;
        }
        visited.insert(currentKey);

        // Dynamically generate possible actions from the current state.
        auto actions = generateActions(current.state);
        for (const auto &action : actions) {
            if (action->isApplicable(current.state)) {
                // Compute the new state by applying the action's effects.
                State newState = current.state;
                action->applyEffects(newState);

                float newCost = current.costSoFar + action->getCost();
                float estimated = newCost + heuristic(newState, goal);

                // Build the new plan node.
                PlanNode next{newState, newCost, estimated, current.actions};
                next.actions.push_back(action);

                std::string newStateKey = stateToString(newState);
                if (visited.find(newStateKey) == visited.end()) {
                    frontier.push(next);
                }
            }
        }
    }
    // Return an empty plan if no solution is found.
    return {};
}

//
// Example usage: Using the dynamic action generator to perform simple grid-based pathfinding.
// Here we assume that the state contains "x" and "y" coordinates. The generator creates moves
// in the four cardinal directions. You can modify or extend this generator to include other actions.
//
int main() {
    // Define the start and goal states.
    State start{{"x", 0}, {"y", 0}, {"enemyInRange", 0}};
    State goal{{"x", 2}, {"y", 2}};

    // Define a dynamic action generator lambda.
    auto generateActions = [](const State &state) -> std::vector<std::shared_ptr<Action>> {
        std::vector<std::shared_ptr<Action>> actions;
        // For pathfinding, generate move actions in four directions.
        int x = state.count("x") ? state.at("x") : 0;
        int y = state.count("y") ? state.at("y") : 0;
        
        // Create moves for each direction.
        actions.push_back(std::make_shared<MoveAction>(x + 1, y)); // Move right.
        actions.push_back(std::make_shared<MoveAction>(x - 1, y)); // Move left.
        actions.push_back(std::make_shared<MoveAction>(x, y + 1)); // Move down.
        actions.push_back(std::make_shared<MoveAction>(x, y - 1)); // Move up.
        
        // Optionally, if an enemy is nearby, include an attack action.
        if (state.find("enemyInRange") != state.end() && state.at("enemyInRange") == 1) {
            actions.push_back(std::make_shared<AttackAction>("Goblin"));
        }
        
        return actions;
    };

    // Run the planner with the dynamic action generator.
    auto plan = planGOAPDynamic(start, goal, generateActions);
    if (plan.empty()) {
        std::cout << "No plan found." << std::endl;
    } else {
        std::cout << "Plan found:" << std::endl;
        for (const auto &action : plan) {
            std::cout << action->getName() << std::endl;
        }
    }

    return 0;
}

```