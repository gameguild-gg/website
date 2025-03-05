# Multi Agent Systems and Planners

## Introduction

We already covered briefly multi agents when we covered the topic agents. In this lecture we are going to cover the topic in more detail but applied to planners.

## 1. Introduction

In its classical form, GOAP is often used for single-agent planning where actions are assumed to be atomic—occurring instantaneously without interfering with one another. Naturally, when multiple agents share the same environment, planning becomes significantly more complex. Agents not only have to reason about their own actions but also account for the fact that their actions might block resources (like grid positions or shared resources) for a period of time.

In a multi-agent setting, actions have **durations** and can **block time slots** and **spaces** or **temporal state block**. For example, while an agent is walking, it will plan ahead that at time X it will be at position Z. Other agents must then consider that position Z will be unavailable at that time.

- Try and repeat, fallback, repulse if it is too close;
- Can we create a flowfield for generalized actions?
- Hierarquical grouping, swarming;
- Leader voting. Follow the leader
- Time, space, state slotting;

---

## 2. Incorporating Time into the Planning Process

### 2.1. Adding a Temporal Dimension

The first step to extend GOAP for multi-agent coordination is to add **time** as an explicit dimension in your state representation. Traditionally, a state might include properties like position, health, or inventory. To accommodate multi-agent interactions:

- **Time Variable**: Augment your state with a “time” variable (or even a timeline) that indicates the simulation or execution time.
- **Temporal Effects**: Modify your actions so that each action not only changes the state (for instance, moving to a new position) but also advances the time by the duration of the action.

### 2.2. Action Duration and Scheduling

Each action should now be defined with an associated duration:
- **Duration**: The time it takes to perform the action.
- **Time Slot Occupancy**: When an action is planned, it reserves a specific time slot (or interval) for its effects to occur. For example, if an agent takes 3 seconds to move from A to B, then the state at time T + 3 must reflect the agent’s new position.
- **Blocking Resources**: As a consequence, the position (or any resource) occupied by an agent becomes blocked for the duration of the action. Other agents must then plan their actions around these reservations.

---

## 3. Modifying the GOAP Framework

### 3.1. Extending the State Representation

To include time, you might modify your state structure as follows:

``` cpp
using State = std::unordered_map<std::string, int>;
// Extend with a time key:
State currentState = { {"x", 0}, {"y", 0}, {"time", 0}, ... };
```

Now, actions will also update the `time` key. For example, a move action that takes 1 time unit might update the state like this:

``` cpp
// Pseudo-code inside the action's applyEffects:
state["x"] = targetX;
state["y"] = targetY;
state["time"] += duration;  // duration > 0
```

### 3.2. Defining Actions with Duration

Actions now need to encapsulate not just preconditions and effects, but also a duration. For instance:

- MoveAction: Moving from one cell to another takes a certain number of time units.
- AttackAction: Engaging an enemy might block a specific action window.

- Each action’s cost could be adjusted to include both the resource cost and the time cost.

### 3.3. Planning with Time Slots

When an agent generates a plan:

- Simulated Time Progression: Each node in the planning search should include the current time. The heuristic can be modified to include not only spatial distance (e.g., Manhattan distance) but also the elapsed time.
- Reservation Table: A central or distributed “reservation table” can record the occupation of key resources (like grid positions) over time. When an agent plans to move to a new position at time T, it must first check the reservation table to ensure no other agent has already reserved that slot.

## 4. Coordination Among Agents

### 4.1. Communication of Planned Actions

For multiple agents to plan effectively:

- Plan Sharing: Agents might share their intended plans (or just their reservations) so that others can avoid conflicts. For instance, if Agent A knows that it will occupy cell Z at time X, it broadcasts this reservation.
- Conflict Resolution: When conflicts occur (e.g., two agents plan to occupy the same cell at the same time), conflict resolution strategies must be employed. This could involve re-planning, priority rules, or negotiation between agents.

### 4.2. Using a Reservation System

A reservation system serves as a central coordinator (or a decentralized mechanism) that:

- Keeps track of which positions (or resources) are reserved at which times.
- Allows agents to query whether a given time slot is free.
- Provides a mechanism to update reservations when plans change or when an agent re-plans its path.

- For example, the reservation table might map positions and time intervals to an agent identifier:

```
ReservationTable = {
("Z", timeX): AgentA,
("Y", timeX+1): AgentB,
...
}
```

Before executing a move, an agent checks the table to ensure that its planned slot is not already taken.

## 5. Modifications to the Planning Algorithm

To support these multi-agent extensions, the planning algorithm itself must be adapted:

- State Nodes: Each node in the search tree now includes a “time” attribute.
- Heuristic Function: The heuristic must account for both spatial distance and time. For example, a combination of Manhattan distance and expected waiting time due to blocked slots.
- Action Generation: The dynamic action generation function now considers the current time. It should generate actions that account for available time slots and avoid conflicts with reservations.
- Cost Function: The cost of each action should reflect not only the effort or risk but also the time spent. This helps the planner avoid unnecessarily long or conflicting plans.

By integrating these modifications, each plan produced by the planner includes a timeline:
`At time T, the agent will be at position X, and at time T+duration, it will transition to position Y.`

This information is critical for other agents to perform their own planning without collisions.

## 6. Considerations and Challenges

### 6.1. Scalability

Adding a temporal dimension increases the state space dramatically. Efficient search algorithms and pruning techniques become even more critical when planning over both space and time.

### 6.2. Uncertainty and Dynamic Environments

In real-world applications, the exact duration of actions might vary, and unexpected events may occur. Incorporating probabilistic models or re-planning mechanisms (dynamic re-planning) can help mitigate these uncertainties.

### 6.3. Decentralized vs. Centralized Coordination

- Centralized Coordination: A central system can manage reservations and resolve conflicts but might become a bottleneck in large-scale systems.
- Decentralized Coordination: Each agent manages its own reservations and communicates with others. This approach is more scalable but requires robust communication protocols.

### 7. Conclusion

Modifying GOAP for a multi-agent environment requires a shift in perspective—from instantaneous actions to actions that take time and occupy space over intervals. By integrating a temporal dimension, incorporating action durations, and establishing a reservation system for resources, agents can plan their actions in a way that minimizes conflicts and improves overall coordination.

This multi-agent extension not only increases the realism of the simulation (or game) but also allows agents to act more intelligently in dynamic, shared environments where every action has both a spatial and temporal footprint. Through careful design of state representations, action definitions, and planning algorithms, we can achieve a robust multi-agent system that balances individual objectives with the collective needs of the environment.