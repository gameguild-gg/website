# **Chess Move Generation Assignment**

## **Objective:**  
Develop a chess bot that can generate legal chess moves based on the current board state, following the official rules as specified at [GameGuild Chess Rules](https://gameguild.gg/chess). The bot does not need to implement a full chess engine but should correctly output legal moves.

---

## **Requirements:**  

Your chess bot should:

- Read the current FEN game state and output a legal move.
- Follow standard chess rules.
- Be implemented in C++.

## **Implementation Guidelines:**

1. **Random Move Bot (Baseline)**: At minimum, your bot should generate random legal moves. You may use external libraries to assist with move generation, but if you do so, it will not receive full points.
2. **Interface**: The bot should take in the current board state (in a standard format such as FEN or PGN) and output the next legal move.
3. **Testing**: Your bot should be able to generate valid moves repeatedly without errors.
4. **Submit on the platform**: Your code should be submitted on the course platform. Ensure that your code is well-documented and includes comments explaining your logic.

**Submission Format:**
- On canvas you should submit a ZIP file containing the whole repository with your source code **without binaries**. 
- On gameguild you should submit only the chess bot code as explained on the platform.

---

**Grading Criteria (10 points total):**
- **Compiles & Runs:** +5 points (Your code compiles and executes correctly.)
- **Random Move Generation:** +3 points (Your bot successfully generates and outputs legal random moves.)
- **Custom Move Generation:** +2 points (You implemented a custom move generator instead of using external chess libraries, such as disservin's chess library.)
