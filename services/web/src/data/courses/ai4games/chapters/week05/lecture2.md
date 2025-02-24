``` c#
using System;
using System.Collections.Generic;
using System.Linq;

// Using State as a dictionary of key-value pairs.
typealias State = Dictionary<string, int>;

// Utility function to check if a state satisfies a condition.
bool Satisfies(State current, State conditions)
{
    return conditions.All(kv => current.TryGetValue(kv.Key, out var val) && val == kv.Value);
}

// Returns a string representation of a State.
string StateToString(State state)
{
    return string.Join(";", state.OrderBy(kv => kv.Key).Select(kv => $"{kv.Key}:{kv.Value}"));
}

// Base class for GOAP actions.
abstract class Action
{
    public abstract State GetPreconditions();
    public abstract State GetEffects();
    public abstract float GetCost();
    public abstract string GetName();
    
    public bool IsApplicable(State state) => Satisfies(state, GetPreconditions());
    
    public virtual void ApplyEffects(State state)
    {
        foreach (var kv in GetEffects())
        {
            state[kv.Key] = kv.Value;
        }
    }
}

// A sample MoveAction for pathfinding.
class MoveAction : Action
{
    private int TargetX, TargetY;
    private float Cost;

    public MoveAction(int targetX, int targetY, float cost = 1.0f)
    {
        TargetX = targetX;
        TargetY = targetY;
        Cost = cost;
    }

    public override State GetPreconditions() => new State();
    public override State GetEffects() => new State { { "x", TargetX }, { "y", TargetY } };
    public override float GetCost() => Cost;
    public override string GetName() => $"Move to ({TargetX}, {TargetY})";
}

// A sample AttackAction for a combat scenario.
class AttackAction : Action
{
    private string Target;
    private float Cost;

    public AttackAction(string target, float cost = 2.0f)
    {
        Target = target;
        Cost = cost;
    }

    public override State GetPreconditions() => new State { { "enemyInRange", 1 } };
    public override State GetEffects() => new State { { "enemyHealth", -10 } };
    public override float GetCost() => Cost;
    public override string GetName() => $"Attack {Target}";
}

// Node structure used by the planning algorithm.
class PlanNode : IComparable<PlanNode>
{
    public State State;
    public float CostSoFar;
    public float EstimatedTotalCost;
    public List<Action> Actions;

    public PlanNode(State state, float costSoFar, float estimatedTotalCost, List<Action> actions)
    {
        State = new State(state);
        CostSoFar = costSoFar;
        EstimatedTotalCost = estimatedTotalCost;
        Actions = new List<Action>(actions);
    }

    public int CompareTo(PlanNode other) => EstimatedTotalCost.CompareTo(other.EstimatedTotalCost);
}

// Heuristic function for estimated cost from current state to goal.
float Heuristic(State current, State goal)
{
    if (current.ContainsKey("x") && current.ContainsKey("y") && goal.ContainsKey("x") && goal.ContainsKey("y"))
    {
        int dx = Math.Abs(current["x"] - goal["x"]);
        int dy = Math.Abs(current["y"] - goal["y"]);
        return dx + dy;
    }
    return 0.0f;
}

// GOAP Planner that dynamically generates actions.
List<Action> PlanGOAPDynamic(State start, State goal, Func<State, List<Action>> generateActions)
{
    var frontier = new SortedSet<PlanNode>(Comparer<PlanNode>.Create((a, b) => a.CompareTo(b)));
    var visited = new HashSet<string>();

    frontier.Add(new PlanNode(start, 0.0f, Heuristic(start, goal), new List<Action>()));

    while (frontier.Count > 0)
    {
        var current = frontier.Min;
        frontier.Remove(current);

        if (Satisfies(current.State, goal))
            return current.Actions;

        string currentKey = StateToString(current.State);
        if (visited.Contains(currentKey))
            continue;
        visited.Add(currentKey);

        foreach (var action in generateActions(current.State))
        {
            if (action.IsApplicable(current.State))
            {
                var newState = new State(current.State);
                action.ApplyEffects(newState);
                
                float newCost = current.CostSoFar + action.GetCost();
                float estimated = newCost + Heuristic(newState, goal);
                
                var next = new PlanNode(newState, newCost, estimated, new List<Action>(current.Actions) { action });
                
                if (!visited.Contains(StateToString(newState)))
                    frontier.Add(next);
            }
        }
    }
    return new List<Action>();
}

// Example usage: Generating actions dynamically.
void Main()
{
    State start = new State { { "x", 0 }, { "y", 0 }, { "enemyInRange", 0 } };
    State goal = new State { { "x", 2 }, { "y", 2 } };

    List<Action> GenerateActions(State state)
    {
        var actions = new List<Action>();
        int x = state.ContainsKey("x") ? state["x"] : 0;
        int y = state.ContainsKey("y") ? state["y"] : 0;
        
        actions.Add(new MoveAction(x + 1, y));
        actions.Add(new MoveAction(x - 1, y));
        actions.Add(new MoveAction(x, y + 1));
        actions.Add(new MoveAction(x, y - 1));

        if (state.TryGetValue("enemyInRange", out int value) && value == 1)
            actions.Add(new AttackAction("Goblin"));
        
        return actions;
    }

    List<Action> plan = PlanGOAPDynamic(start, goal, GenerateActions);
    Console.WriteLine("Generated Plan:");
    foreach (var action in plan)
    {
        Console.WriteLine(action.GetName());
    }
}
```