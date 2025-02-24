# Nested loops

Nested loops are loops within loops. They are useful when you work with multi-dimensional data structures like matrices or grids.

``` python
matrix = [[1, 2, 3], [4, 5, 6], [7, 8, 9]]
for row in matrix:
    for value in row:
        print(value, end=" ") # prints without newline
    print()  # Newline after each row
```

- The outer loop iterates over each row.
- The inner loop iterates over each element within the current row.
- Nested loops increase the time complexity, so use them judiciously.

!!! code
{
"description": "Implement a function to print a 2D list (matrix) in a grid format. If the number is odd, print it doubled. If even, print it divided by 2.",
"language": "python",
"code": "def print_matrix(matrix):\n    # your code goes here\n\n# do not modify the code below\nprint_matrix([[1, 2, 3], [4, 5, 6], [7, 8, 9]])",
"expectedOutput": "2 1.0 6 2.0 10 3.0 14 4.0 18"
}
!!!

## Breaking Nested Loops

Using the `break` statement in a nested loop only exits the innermost loop. To break out of multiple loops at once, consider these approaches:

#### Using a Flag

``` python
found = False
for i in range(5):
    for j in range(5):
        if i * j > 6:
            found = True
            break  # Exits the inner loop
    if found:
        break  # Exits the outer loop
print("Exited nested loops using a flag.")
```

#### Using a Function return

Wrapping your nested loops in a function allows you to exit all loops at once by using return:

``` python
def find_product_exceeding(limit):
    for i in range(5):
        for j in range(5):
            if i * j > limit:
                return (i, j)  # Immediately exits the function
    return None

result = find_product_exceeding(6)
if result:
    print(f"Found a pair {result} where the product exceeds 6.")
else:
    print("No such pair found.")
```

- Flag Method: Works but can clutter your code.
- Function with `return`: More elegant and encapsulates the loop logic, making your code easier to manage and test.

!!! code
{
"description": "Implement a function to find and return the first pair of numbers in a list that it sum is exactly a target.",
"language": "python",
"code": "def find_pair_with_sum(numbers, target):\n    # your code goes here\n\n# do not modify the code below\nprint(find_pair_with_sum([1, 2, 3, 4, 5], 7))",
"expectedOutput": "(2, 5)"
}
!!!

## In class activity

In class we will code a connect-4 game using nested loops and functions. The game will be played in the terminal. Check canvas for details.