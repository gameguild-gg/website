# Algorithms

<details>
<summary> Teacher notes </summary>
- Icebreakers; Intro to the Instructor; Form pairs and Introduce each other; Fill the form about python; Introduce the class routines: weekly activities, read before class, assignments, etc.; Introduce canvas; Create github account; Install tools such as pycharm, python 3.11, and git;
- Intro to Algorithmic thinking; What is an algorithm?; Example: Sorting; Example: Mazes;
</details>

## What is an algorithm?

An algorithm is an ordered sed of instructions used to solve a problem or perform a task. Think of an algorithm as a recipe. A recipe is a set of instructions that tells you how to prepare a dish.

The quality of an algorithm is determined by how well it solves a problem and how efficiently it does so. An efficient algorithm is one that uses the fewest resources (time, memory, etc.) to solve a problem.

## Example: Sorting

The most common way to understand algorithms is through sorting. Sorting is the process of arranging items in a specific order. For example, sorting a list of numbers in ascending order, or even how can you sort a deck of card? How would you do that?

Check this animation and explore different types of sorting algorithms, but please do not try to understand the internals of it, just get a sense of how they work. Try to revers engineer the algorithm by looking at the animation. Focus on the Insertion Sort first and when you feel confident, try to write the recipe for it and share if with your friends.

- Visualize it here: [Visualgo](https://visualgo.net/en/sorting)
- Or visualize it here: [Sorting Algorithms](https://www.toptal.com/developers/sorting-algorithms)

## Example: Mazes

Another way to understand algorithms is through maze-solving. A maze is a puzzle that consists of a series of walls and passages. The goal is to find a path from the start to the end of the maze.

![Maze](https://upload.wikimedia.org/wikipedia/commons/thumb/8/88/Maze_simple.svg/1200px-Maze_simple.svg.png)

Imagine you are on this maze and you need to find the way out. How would you do that? What steps would you take? What rules would you follow?

---

### Maze solving algorithms: Random Approach

1. Move forward until you have choices;
2. Choose a random direction;
3. If you face a dead-end, turn back and go to the previous location;
4. Repeat steps 1-3 until you find the exit.

How efficient is this algorithm? What are the pros and cons of this approach?

<details>
  <summary> Answer </summary>
  <p> This algorithm is not very efficient because it relies on randomness. It may take a long time to find the exit, especially in complex mazes. However, it is simple to implement and does not require much memory. </p>
</details>

---

### Maze solving algorithms: Right-hand Rule

1. Move forward until you have choices;
2. Choose the right-hand direction;
3. If you face a dead-end, turn back and go to the previous location;
4. Repeat steps 1-3 until you find the exit.

Is this algorithm more efficient than the random approach? What are the pros and cons of this approach? Does it always work?

<details>
  <summary> Answer </summary>
  <p> This algorithm is more efficient than the random approach because it follows a systematic rule. It guarantees that you will eventually find the exit if it exists. However, it may not work in all mazes, especially if there are loops or cycles. </p>
</details>

---

### Maze solving algorithms: Tremaux's Algorithm

The idea is similar to the tale of Theseus and the Minotaur. Theseus used a ball of thread to find his way out of the labyrinth. Tremaux's Algorithm is based on the same principle.

1. Start at the entrance of the maze.
    - Walk into the first path and begin exploring.
2. Mark the path you are walking on.
    - If it’s the first time you are walking on this path, leave one mark (e.g., draw a line or place a small object).
    - If you are walking on a path you’ve already marked once, add a second mark.
3. At a junction (a place with multiple paths):
    - Always pick an unmarked path if there is one.
    - If all paths have been marked once, pick a path with only one mark (never take a path marked twice unless you’re backtracking).
4. Dead end (no unmarked paths):
    - Turn around and go back the way you came, following the marks you left.
5. Continue exploring:
    - Keep moving through the maze, marking paths as you go, until you either find the exit or have explored every possible path.
6. If you reach the exit, you’re done!
    - If there’s no way forward and all paths are marked twice, the maze has no exit.

What are the pros and cons of this approach? How efficient is it compared to the previous algorithms?

<details>
  <summary> Answer </summary>
  <p> Tremaux's Algorithm is more systematic than the previous algorithms because it keeps track of the paths you have explored. It guarantees that you will eventually find the exit if it exists. However, it may require more memory to store the marks. </p>
</details>

---

Mazes in games: Zelda ToTK Lomei Sky Labirinth:

![Zelda Lomei Sky Labirinth](https://platform.polygon.com/wp-content/uploads/sites/2/chorus/uploads/chorus_asset/file/24696220/My_Great_Game___My_Great_Capture_2023_06_01_09_32_06__1_.png?quality=90&strip=all&crop=0%2C0%2C100%2C100&w=750)
[Source](https://www.polygon.com/zelda-tears-of-the-kingdom-guide/23745317/lomei-labyrinth-island-walkthrough-igashuk-mogisari-shrine-puzzle-solution)

---

## Algorithmic thinking

It is the ability to analyze given problems:

- the ability to specify a problem precisely
- the ability to find the basic actions that are adequate to the given problem
- the ability to construct a correct algorithm to a given problem using the basic
  actions
- the ability to think about all possible special and normal cases of a problem
- the ability to improve the efficiency of an algorithm

## Assignment

Solve all the mazes from [Blockly Maze](https://blockly.games/maze) using your algorithmic thinking and the strategies you learned today. Store the links of your solutions and share them with your professor.