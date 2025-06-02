# Python Conditionals and Loops

## 1. Introduction to Conditionals
Conditionals allow your program to make decisions based on certain conditions. In Python, we use `if`, `elif`, and `else` statements to control the flow of execution.

::: tip "Why Use Conditionals?"
Conditionals allow programs to behave differently based on inputs or computed values. They are fundamental in decision-making processes.
:::

### 1.1 Basic `if` Statement
``` python
x = 10
if x > 5:
    print("x is greater than 5")
```

### 1.2 `if-else` Statement
``` python
x = 3
if x > 5:
    print("x is greater than 5")
else:
    print("x is not greater than 5")
```

!!! quiz
{
"title": "Basic `if-else` Quiz",
"question": "What will be printed when `x = 3` in the following code?\n\n```python\nif x > 5:\n    print(\"Greater than 5\")\nelse:\n    print(\"5 or less\")\n```",
"options": ["Greater than 5", "5 or less", "No output", "Error"],
"answers": ["5 or less"]
}
!!!


### 1.3 `if-elif-else` Statement
``` python
x = 7
if x > 10:
    print("x is greater than 10")
elif x > 5:
    print("x is greater than 5 but not greater than 10")
else:
    print("x is 5 or less")
```

!!! quiz
{
  "title": "Basic Conditional Quiz",
  "question": "What will be printed when `x = 7` in the following code?\n\n```python\nif x > 10:\n    print(\"Greater than 10\")\nelif x > 5:\n    print(\"Greater than 5\")\nelse:\n    print(\"5 or less\")\n```",
  "options": ["Greater than 10", "Greater than 5", "5 or less", "No output"],
  "answers": ["Greater than 5"]
}
!!!

## 2. Nested Conditionals
Conditionals can be nested within each other to evaluate multiple conditions.

``` python
x = 15
if x > 10:
    if x < 20:
        print("x is between 10 and 20")
```

::: tip "Avoid Deep Nesting"
Excessive nesting can make your code harder to read. Consider using logical operators (`and`, `or`) instead.
:::

!!! quiz
{
"title": "Nested Conditional Quiz",
"question": "What will be printed when `x = 15` in the following code?\n\n```python\nif x > 10:\n    if x < 20:\n        print(\"x is between 10 and 20\")\n```",
"options": ["x is between 10 and 20", "No output", "x is greater than 10", "Error"],
"answers": ["x is between 10 and 20"]
}
!!!


## 3. Short-Circuiting
Python evaluates conditions from left to right and stops as soon as the result is determined. This is known as *short-circuiting*.

``` python
def check_a():
    print("Checking a")
    return False

def check_b():
    print("Checking b")
    return True

if check_a() and check_b():
    print("Both are True")
else:
    print("At least one is False")
```

In this case, `check_b()` is **not executed** because `check_a()` returns `False`, and `and` requires both conditions to be `True`.

!!! quiz
{
  "title": "Short-Circuiting Quiz",
  "question": "What will be printed if `check_a()` returns `False`?",
  "options": ["Checking a", "Checking b", "Both are True", "At least one is False"],
  "answers": ["Checking a", "At least one is False"]
}
!!!

## 4. Loops in Python
Loops are used to execute a block of code multiple times.

### 4.1 `while` Loop
A `while` loop continues to execute as long as the condition remains `True`.

``` python
x = 0
while x < 5:
    print(x)
    x += 1
```

!!! quiz
{
"title": "Basic `while` Loop Quiz",
"question": "What will be printed by the following code?\n\n```python\nx = 0\nwhile x < 3:\n    print(x)\n    x += 1\n```",
"options": ["0 1 2", "0 1 2 3", "3", "No output"],
"answers": ["0 1 2"]
}
!!!


### 4.2 `for` Loop
A `for` loop iterates over a sequence (such as a list, range, or string).

``` python
for i in range(5):
    print(i)
```

### 4.3 Loop Control Statements
- **`break`**: Exits the loop immediately.
- **`continue`**: Skips the current iteration and proceeds to the next.
- **`pass`**: Placeholder that does nothing.

``` python
for i in range(5):
    if i == 3:
        break
    print(i)  # Output: 0, 1, 2
```

``` python
for i in range(5):
    if i == 3:
        continue
    print(i)  # Output: 0, 1, 2, 4
```

!!! quiz
{
  "title": "Loop Quiz",
  "question": "What will be the output of the following code?\n\n```python\nfor i in range(5):\n    if i == 2:\n        break\n    print(i)\n```",
  "options": ["0 1 2 3 4", "0 1", "2 3 4", "No output"],
  "answers": ["0 1"]
}
!!!

## 5. Summary
- Use `if`, `elif`, and `else` to create decision-making logic.
- Nest conditionals only when necessary.
- Short-circuiting optimizes logical evaluations.
- `while` loops repeat while a condition is `True`.
- `for` loops iterate over a sequence.
- Use `break`, `continue`, and `pass` for finer loop control.

