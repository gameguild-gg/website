# Intro to python

<details>
<summary> Teacher notes </summary>

- Python REPL: Read-Eval-Print Loop;
- Python basic operators: `+`, `-`, `*`, `/`, `//`, `%`, `**`;
- Python variables: `int`, `float`, `str`, `bool`;
- Python terminal: How to run python scripts;

</details>

Python basics. By the end of this lecture, you will understand Python's interactive environment, fundamental operators,
variables, and how to interact with the terminal. Let’s get started!

---

## Python REPL: Read-Eval-Print Loop

The Python REPL (Read-Eval-Print Loop) is an interactive shell that lets you test small pieces of code quickly. To start
the REPL, simply type `python` or `python3` in your terminal.

- **Read:** Takes user input.
- **Eval:** Evaluates the input as Python code.
- **Print:** Displays the result.
- **Loop:** Repeats the process.

### Example:

``` python
>>> 2 + 2
4
>>> print("Hello, world!")
Hello, world!
```

Use the REPL to experiment with Python commands, expressions, and functions.

---

## Basic Operators

Python provides a variety of operators for arithmetic and other computations. Here are some of the most common ones:

| Operator | Description             | Example  | Result |
|----------|-------------------------|----------|--------|
| `+`      | Addition                | `2 + 3`  | `5`    |
| `-`      | Subtraction             | `5 - 2`  | `3`    |
| `*`      | Multiplication          | `4 * 3`  | `12`   |
| `/`      | Division (float result) | `7 / 2`  | `3.5`  |
| `//`     | Floor division (int)    | `7 // 2` | `3`    |
| `%`      | Modulus (remainder)     | `7 % 2`  | `1`    |
| `**`     | Exponentiation          | `2 ** 3` | `8`    |

Test these operators in the REPL to see their behavior.

---

## Variables as Containers

In Python, variables act as containers that store data values. To create a variable, simply assign a value to a name
using the `=` operator.

### Example:

``` python
x = 10
name = "Alice"
is_active = True
```

### Rules for Naming Variables:

1. Variable names must start with a letter 🔤 or underscore (`_`). They cannot 🚫 start with a number.
2. Only alphanumeric characters and underscores are allowed.
3. Variable names are case-sensitive (`age` and `Age` are different).

example:

``` python
age = 25          # Valid
_name = "Bob"     # Valid
1st_name = "John" # Invalid
```

::: tip
Use descriptive variable names to make your code more readable and maintainable.
:::

Good naming practices:

``` python
first_name = "Alice"
total_score = 100
is_active = True
```

Bad naming practices:

``` python
a = "Alice"
b = 100
i = True
```

---

## Type: Dynamic Inference vs Explicit Declaration

Python uses **dynamic typing**, meaning you don't need to declare variable types explicitly. The interpreter infers the
type based on the value assigned.

### Example:

``` python
age = 25           # Inferred as int
name = "Alice"    # Inferred as str
is_active = True   # Inferred as bool
```

However, Python also supports **explicit type annotations** (introduced in Python 3.6) for better clarity and type
checking:

### Example:

``` python
age: int = 25
name: str = "Alice"
is_active: bool = True
```

Explicit type annotations are optional but can help improve code readability and maintainability by preventing
type-related and obscure bugs.

---

## Crazy Operations: "hello" * 10

Python allows some unexpected operations! For example:

### Example:

``` python
>>> "hello" * 10
'hellohellohellohellohellohellohellohellohellohello'
```

- When multiplying a string by an integer, Python repeats the string.
- Multiplying a string by `0` or a negative number results in an empty string:

``` python
>>> "hello" * 0
''
>>> "hello" * -1
''
```

Try experimenting with other combinations in the REPL.

---

## Read and Write from/to the Terminal

Python provides built-in functions to interact with the terminal:

### Reading Input:

Use the `input()` function to read user input as a string.

``` python
name = input("Enter your name: ")
```

- The input function always returns a string, so you may need to convert it to another type if necessary.

### Writing Output:

Use the `print()` function to display information in the terminal.

``` python
print(f"Hello, {name}!")
```

- The `f` before the string enables **string interpolation**, allowing you to embed variables directly within the
  string.

---

## Putting It All Together

### Example Program:

``` python
# Simple Greeting Program
name = input("Enter your name: ")
print(f"Hello, {name}!")

# Age Calculator
age = int(input("Enter your age: "))
print(f"Next year, you will be {age + 1} years old!")
```

---

## Homework:

Do all the assignments on canvas.