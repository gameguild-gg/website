# Hierarchical Task Networks

## 1. Recap: GOAP Actions as the Building Blocks

Recall that in GOAP you learned to represent the world as a collection of states and actions. Each action in GOAP is defined by:

- **Preconditions**: Conditions that must hold true for the action to be applied.
- **Effects**: Changes in state after the action is executed.
- **Cost**: A value that guides the planner to prefer certain actions.
- **Name**: A unique identifier, used for debugging and planning.

In GOAP, the planning process involves a search (inspired by A*) over these independent actions to generate a plan from the current state to a goal state.

## 2. Evolving GOAP into HTN: From Independent Actions to Decomposable Tasks

In HTN planning, we take the same GOAP actions and raise the level of abstraction:

- **Primitive Tasks**: These directly correspond to the GOAP actions. They are “atomic” and can be executed directly.
- **Compound Tasks**: Instead of selecting actions randomly from a flat list, compound tasks are high-level objectives that are decomposed into a series of subtasks. These subtasks may include primitive tasks (the GOAP actions) or even other compound tasks.

By embedding domain knowledge into the HTN planner, we can define methods—structured recipes that decompose a compound task into a sequence or network of subtasks. This hierarchy allows us to:

- Organize and reuse knowledge more naturally.
- Reduce the search space by leveraging known task decompositions.
- Create plans that are not only valid but also intuitively aligned with human reasoning.

## 3. HTN Planning: Building on GOAP Actions

### 3.1. From GOAP Actions to Primitive Tasks

In GOAP, you defined independent actions—such as `MoveAction` and `AttackAction`, each with their own preconditions, effects, costs, and unique identifiers. In HTN planning, these actions transform into primitive tasks. They retain all the detailed semantics of the original GOAP actions and serve as the atomic, executable units at the bottom of the hierarchical structure.

### 3.2. Compound Tasks and Methods: Structuring Higher-Level Goals

While GOAP presents a flat collection of actions, HTN planning introduces compound tasks that represent higher-level objectives. These compound tasks are not directly executable; they require decomposition into subtasks, which may be either primitive (directly mapped from GOAP actions) or even other compound tasks.

For example, consider a compound task like `SecureArea`. This high-level objective might be decomposed into:

- Task 1: **MoveToLocation** (a primitive task derived from a GOAP MoveAction).
- Task 2: **EliminateThreat** (a primitive task derived from a GOAP AttackAction).

This decomposition is achieved through methods—domain-specific recipes that dictate how a compound task should be broken down into its constituent subtasks. Each method typically includes:

- The name of the compound task it applies to.
- A prescribed sequence or set of subtasks.
- Optional preconditions that determine when this particular method is appropriate.

Methods leverage existing domain knowledge to structure the planning problem, reducing the search space and generating plans that are both efficient and intuitively aligned with real-world scenarios.

### 3.3. Task Networks: Organizing and Managing Decompositions

A central concept in HTN planning is the task network. Unlike a simple list of tasks, a task network is a structured representation that captures the relationships, dependencies, and ordering constraints among tasks. Here are some key aspects of task networks:

- **Ordering Constraints**: The network defines the sequence in which tasks must be executed. Some tasks are strictly sequential, while others may be performed in parallel if they do not depend on one another.
- **Dependency Relationships**: Task networks capture dependencies—certain tasks may only be initiated once others are completed. This ensures that prerequisites are met and that the plan flows logically.
- **Hierarchical Structure**: The task network reflects the hierarchical decomposition process. At the top level, you have the compound task (e.g., "SecureArea"), and as you decompose it, the network branches into subtasks until only primitive tasks (the GOAP actions) remain.
- **Flexibility and Adaptability**: By organizing tasks into a network, the planner can more easily adapt to changes. If an unexpected event occurs, the network can be re-evaluated, and tasks can be re-ordered while preserving critical dependencies.

When you construct a task network from GOAP-derived primitive tasks and compound tasks, you’re essentially embedding domain knowledge into the structure of the plan. The network not only dictates the execution sequence but also ensures that the plan is coherent and robust in the face of dynamic challenges.

## 4. Implementing an HTN Planner in C++ Using GOAP Actions

We will extend our previous GOAP implementation, where actions are independent state transitions—by constructing an HTN planner that builds a structured task network. Here, our familiar GOAP actions serve as the primitive tasks, and we introduce compound tasks along with methods that decompose these tasks into ordered subtasks. The resulting task network not only sequences tasks but also captures dependencies and ordering constraints inherent in hierarchical planning.

### 4.1. Defining Task and Method Structures

We start by defining a basic Task structure. Each task can be either:

- **Primitive**: Directly corresponding to a GOAP action (e.g., "MoveToLocation" or "EliminateThreat").
- **Compound**: Representing a higher-level goal that requires decomposition.

A Method specifies how to decompose a compound task into an ordered list of subtasks, thereby forming the task network.

``` cpp
// Base Task represents both primitive tasks (from GOAP actions) and compound tasks.
struct Task {
    string name;
    bool isPrimitive;  // True if this task is a GOAP action.

    Task(const string &n, bool primitive)
        : name(n), isPrimitive(primitive) {}
};

// A Method defines how to decompose a compound task into an ordered list of subtasks.
struct Method {
    string compoundTaskName; // The compound task this method decomposes.
    vector<shared_ptr<Task>> subtasks; // The ordered list of subtasks.

    Method(const string &taskName, const vector<shared_ptr<Task>> &subs)
        : compoundTaskName(taskName), subtasks(subs) {}
};
```

### 4.2. Recursive Task Decomposition and Task Network Formation

The heart of our HTN planner is a recursive function that decomposes a compound task into a network of primitive tasks. This process respects the ordering defined in each method, thereby constructing a task network where:

- **Ordering Constraints** ensure that tasks are executed in a specific sequence.
- **Dependencies** between tasks are maintained, so that prerequisites are satisfied before subsequent tasks are executed.

``` cpp
// Recursive function to decompose a compound task into a network of primitive tasks.
bool decomposeTask(const shared_ptr<Task>& task,
                   const vector<Method>& methods,
                   vector<shared_ptr<Task>>& taskNetwork) {
    if (task->isPrimitive) {
        // The primitive task is our base, directly corresponding to a GOAP action.
        taskNetwork.push_back(task);
        return true;
    }
    
    // For compound tasks, search for an applicable method.
    for (const auto& method : methods) {
        if (method.compoundTaskName == task->name) {
            vector<shared_ptr<Task>> localNetwork;
            bool success = true;
            // Decompose each subtask in the prescribed order.
            for (const auto& subtask : method.subtasks) {
                if (!decomposeTask(subtask, methods, localNetwork)) {
                    success = false;
                    break;
                }
            }
            if (success) {
                // Integrate the decomposed subtasks into the task network.
                taskNetwork.insert(taskNetwork.end(), localNetwork.begin(), localNetwork.end());
                return true;
            }
        }
    }
    // No applicable method was found to decompose the compound task.
    return false;
}
```

### 4.3 An Example: Building a Task Network for "SecureArea"

Let’s apply our HTN planner to a simple example. Suppose we have a compound task `SecureArea`, which—based on our domain knowledge—should be decomposed into two primitive tasks:

- **MoveToLocation**: Derived from the GOAP MoveAction.
- **EliminateThreat**: Derived from the GOAP AttackAction.

By defining a method for `SecureArea`, we encode the ordered sequence in which these tasks must be performed, forming a coherent task network.

``` cpp
int main() {
    // Define primitive tasks corresponding to GOAP actions.
    auto moveToLocation = make_shared<Task>("MoveToLocation", true);
    auto eliminateThreat = make_shared<Task>("EliminateThreat", true);
    
    // Define a compound task, e.g., "SecureArea".
    auto secureArea = make_shared<Task>("SecureArea", false);
    
    // Define a method to decompose "SecureArea" into its ordered subtasks.
    vector<Method> methods;
    methods.push_back(Method("SecureArea", {moveToLocation, eliminateThreat}));
    
    // Build the task network by decomposing the compound task.
    vector<shared_ptr<Task>> taskNetwork;
    if (decomposeTask(secureArea, methods, taskNetwork)) {
        cout << "Task Network:" << endl;
        for (const auto &t : taskNetwork) {
            cout << "- " << t->name << endl;
        }
    } else {
        cout << "Failed to generate a task network." << endl;
    }
    
    return 0;
}
```

## 5. HTN Planning Proccess

The planning process in HTN involves several recursive steps that gradually refine a high-level goal into a concrete plan:

- **Start with a High-Level Task**: Begin with an overarching task that encapsulates the goal you want to achieve. This task is typically compound and represents the strategic objective.
- **Task Decomposition**: Look up available methods to decompose the compound task into subtasks. This step uses domain knowledge to determine the best way to break the task down.
- **Network Formation**: As subtasks are identified, build a task network that captures both the order of execution and the dependencies between tasks. This network ensures that the plan respects logical sequencing.
- **Recursive Decomposition**: Apply the decomposition process recursively to each compound subtask. Continue this process until every task in the network is primitive and directly executable.
- **Plan Extraction**: Once the task network consists solely of primitive tasks, extract the sequence. This final plan is a series of executable actions that the agent can perform, ensuring that all higher-level goals are met.

## 6. Conclusion

HTN planning extends the capabilities of traditional GOAP by introducing a hierarchical structure that organizes tasks into compound objectives and subtasks. By leveraging domain-specific knowledge through methods, HTN planners can generate plans that are not only valid but also intuitive and efficient. The resulting task networks capture the logical flow of actions, ensuring that agents can navigate complex scenarios with ease.

Next time, we will deepen the implementation of HTN planners and explore how to apply this powerful technique to solve complex planning problems in game AI.