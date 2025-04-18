# Monte Carlo Tree Search

MCTS is a search algorithm that uses random interactive deepening to estimate the value or score of different actions in a game. It an awesome algorithm for games with large branching factors, such as Go, Chess, and other turn-based games where the number of possible moves is very large.

Instead of searching the entire game tree like Min-Max, MCTS builds a tree incrementally by simulating random games from the current state. Until a specific constraint is met, such as a time limit or a maximum number of iterations. It uses the results of these simulations to update the values of the nodes in the tree.

The algorithm consists of four main steps:

1. **Selection**: Starting from the root node, select child nodes until a leaf node is reached. The selection is based on a balance between exploration and exploitation, often using the Upper Confidence Bound (UCB) formula.
2. **Expansion**: If the leaf node is not a terminal state, expand it by adding one or more child nodes representing possible actions.
3. **Simulation**: Perform a random simulation from the newly added child node to a terminal state. This simulation can be done using a random policy or a more sophisticated heuristic.
4. **Backpropagation**: Update the values of the nodes in the tree based on the result of the simulation. This involves propagating the result back up to the root node, updating the visit counts and win rates.

## Upper Confidence Bound (UCB)

$$UCB = \frac{w_i}{n_i} + C \sqrt{\frac{\ln N}{n_i}}$$

Where:
- $w_i$ is the number of wins for the child node \(i\)
- $n_i$ is the number of visits for the child node \(i\)
- $N$ is the total number of visits for the parent node
- $C$ is a constant that controls the exploration-exploitation trade-off. Usually set to $\sqrt{2}$

::: note "Work in Progress"

I wasn't able to finish the material for this week because I had to improve the renderer to render latex, and graphs, so please use de following materials to deepen your knowledge on the subject.

:::

## Implementation

### Data Structures

``` c++
class MCTSNode {
public:
    // optionally use smart pointers 
    MCTSNode* parent;
    std::vector<MCTSNode*> children;
    int wins;
    int visits;
    // change this to be your game state
    GameState state;

    MCTSNode(GameState state) : parent(nullptr), wins(0), visits(0), state(state) {}

    double ucb() {
        // initialize the UCB value as the maximum value in order to mark them as high priority to visit
        if (visits == 0) return std::numeric_limits<double>::max();
        return ((double)wins / visits) + sqrt(2 * log(parent->visits) / visits);
    }
};
```

### MCTS functions

#### Selection

``` c++
MCTSNode* select(MCTSNode* node) {
    // for all nodes
    while (!node->children.empty()) {
        // Select the child with the highest UCB value
        MCTSNode* bestChild = *std::max_element(node->children.begin(), node->children.end(),
            [](MCTSNode* a, MCTSNode* b) { return a->ucb() < b->ucb(); });
        node = bestChild;
    }
    return node;
}
```

#### Expansion

``` c++
void expand(MCTSNode* node) {
    // Generate all possible moves from the current state
    std::vector<GameState> possibleStates = node->state.getPossibleStates();
    for (const GameState& state : possibleStates) {
        // note that all new child will have a ucb as max value, so the selection will prefer them, this is intentional
        MCTSNode* childNode = new MCTSNode(state);
        childNode->parent = node;
        node->children.push_back(childNode);
    }
}
```

#### Simulation

``` c++
// this can be a boolean, score, the state or whatever you prefer
boolean simulate(MCTSNode* node) {
    GameState currentState = node->state;
    // Simulate a random game until a terminal state is reached
    // or until a certain depth, or iterations, time limit or any heuristic 
    while (!currentState.isTerminal()) {
        // this will be called a lot, thats why your generate possible states should be fast
        std::vector<GameState> possibleStates = currentState.getPossibleStates();
        // Randomly select one of the possible states
        // Here you can use a more sophisticated policy instead of random
        // or if the depth is too deep, you can use a heuristic to decide the "winner" and avoid deepening more
        currentState = possibleStates[rand() % possibleStates.size()];
    }
    return currentState;
}
```

#### Backpropagation

``` c++
void backpropagate(MCTSNode* node, boolean win) {
    // Backpropagate the result up to the root node
    while (node != nullptr) {
        node->visits++;
        if (win)
            node->wins++;
        node = node->parent;
    }
}
```

- [Animation](https://vgarciasc.github.io/mcts-viz/)
- [Text](https://uq.pressbooks.pub/mastering-reinforcement-learning/chapter/monte-carlo-tree-search/)
- [Presentation](https://duvenaud.github.io/learning-to-search/slides/week3/MCTSintro.pdf)


<ul>
  <li><a href="https://arxiv.org/pdf/1705.08439.pdf">Thinking Fast and Slow with Deep Learning and Tree Search</a></li>
  <li><a href="http://discovery.ucl.ac.uk/10045895/1/agz_unformatted_nature.pdf">Mastering the game of Go without Human Knowledge</a></li>
  <li><a href="https://arxiv.org/abs/1712.01815">Mastering Chess and Shogi by Self-Play with a General Reinforcement Learning Algorithm</a></li>
</ul>

Other resources:

<ul>
  <li><a href="http://www.cs.toronto.edu/~rgrosse/courses/csc421_2019/slides/lec22.pdf">Roger Grosse and Jimmy Ba’s MCTS Slides</a></li>
  <li><a href="https://duvenaud.github.io/learn-discrete/slides/Thinking-Fast-and-Slow-with-Deep-Learning-and-Tree-Search.pdf">Alex Adam and Fartash Faghri’s thinking fast and slow slides</a></li>
  <li><a href="https://www.inference.vc/alphago-zero-policy-improvement-and-vector-fields/">Ferenc Huszar’s blog post on Expert Iteration</a></li>
</ul>

