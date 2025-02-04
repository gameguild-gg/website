# GOAP (Goal-Oriented Action Planning) Assignment

## Overview

Your task is to implement a GOAP (Goal-Oriented Action Planning) system in any game engine of your choice or even as a plain terminal-based simulation. You may use the provided base code or build your system entirely from scratch.

This assignment is designed to help you understand AI decision-making using GOAP, with stretch goals available for those who want to push their implementation further.

### Total Points: 60

---

## Minimum Requirements (50 Points Total)
To achieve the base score, your GOAP system must fulfill the following requirements:

1. **State Representation (10 Points)**
    - Implement a state representation as key-value pairs (e.g., health: 100, ammo: 5).

2. **Action System (15 Points)**
    - Create at least **three actions** (e.g., "Move", "Jump", "Dash", "Shoot", "Collect Ammo," "Attack Enemy," "Heal").
    - Each action must have:
        - Preconditions (state requirements for action execution)
        - Effects (state changes after execution)
        - Cost (a value to prioritize actions)

3. **Planner Implementation (15 Points)**
    - Develop a planner that finds an optimal sequence of actions to achieve a given goal.
    - The planner should handle branching paths and select the most cost-effective solution.

4. **Goal System (10 Points)**
    - Implement at least **two different goals** (e.g., "Eliminate Enemy," "Survive for 30 seconds").

5. **User or Simulation Interaction (5 Points)**
    - Demonstrate the GOAP system by showing the sequence of actions planned and executed.
    - This can be output in a terminal or a visual demonstration in a game engine.

6. **Video Demonstration (Mandatory)**
    - Record a short video (3-5 minutes) demonstrating your GOAP system in action.
    - Explain your implementation, walk through the key features, and highlight the action planning.

---

## Stretch Goals (Optional for Extra Points)
Earn additional points by implementing the following stretch goals:

1. **Dynamic Action Costs (5 Points)**
    - Allow action costs to change dynamically based on the current state (e.g., movement cost increases if stamina is low).

2. **Pathfinding Integration (5 Points)**
    - Extend the GOAP system to act as a pathfinder.
    - Example: "Move to (X, Y)" actions with grid-based movement and preconditions for adjacency.

3. **Conflict Resolution (5 Points)**
    - Handle conflicting actions gracefully (e.g., mutually exclusive conditions).

4. **Multiple Agents (5 Points)**
    - Implement GOAP for multiple agents with different goals and actions.

5. **State Visualization (5 Points)**
    - Create a graphical representation of the state transitions or the action plan.

6. **Heuristic Optimization (5 Points)**
    - Implement a heuristic-based optimization to improve planner efficiency.

---

## Submission Guidelines
- Submit your project files, including source code and any assets used.
- Provide a brief README describing how to run your program.
- Include a short write-up explaining your design choices.
- **Submit a video demonstration (3-5 minutes) showcasing your system and key features.**

---

## Grading Rubric
| Requirement                        | Points   |
|------------------------------------|----------|
| State Representation               | 10       |
| Action System                      | 15       |
| Planner Implementation             | 15       |
| Goal System                        | 10       |
| User or Simulation Interaction     | 5        |
| **Video Demonstration**            | Required |
| **Total for Minimum Requirements** | 50       |
| **Stretch Goals (Optional)**       | Up to 10 |
| **Final Maximum Score**            | 60       |

