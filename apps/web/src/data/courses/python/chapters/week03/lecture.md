# Functions

<details>
<summary> Teacher notes </summary>

- Basic functions: `def`, `return`, `print`;
- Comments in functions;
- math module
- Numbers: Binary, Hexadecimal, Negative, Floating point;

</details>

## What Are Functions?

Functions are blocks of reusable code that perform a specific task. In Python, functions allow us to organize code into
logical, manageable chunks. This not only makes our programs easier to read and maintain but also reduces redundancy.

### Key Characteristics:

- Defined using the `def` keyword.
- Can take input arguments.
- May or may not return a value using the `return` keyword.
- Help encapsulate functionality.

::: tip "Why Use Functions?"
Functions make your code modular, reusable, and easier to debug. By dividing logic into smaller pieces, you can work on
specific parts without affecting others.
:::

---

## Defining and Calling Functions

To define a function in Python, use the `def` keyword followed by the function name and parentheses. Indentation is
crucial in Python; the body of the function must be indented consistently.

### Indentation in Functions

Python relies on indentation to define the scope of blocks. In functions, every line of the body must have the same
level of indentation, typically four spaces:

```python
# Correct indentation

def greet():
    print("Hello, world!")

# Incorrect indentation

def greet():
print("Hello, world!")  # This will raise an IndentationError
```

### Function with Arguments

Functions can accept arguments to make them more flexible:

```python
# Function with arguments
def greet_person(name):
    print(f"Hello, {name}!")

# Calling the function
greet_person("Alice")
```

::: tip "Using Default Arguments"
You can provide default values to arguments. This way, if no value is passed, the default is used:

```python
def greet_person(name="Guest"):
    print(f"Hello, {name}!")

greet_person()  # Outputs: Hello, Guest!
```

:::

### Function with a Return Value

Functions can return values using the `return` keyword:

```python
# Function with a return value
def add_numbers(a, b):
    return a + b

result = add_numbers(3, 5)
print("The sum is:", result)
```

### Longer Example

Here’s a more detailed example of a function with multiple lines:

```python
def calculate_average(numbers):
    """
    Calculates the average of a list of numbers.
    Arguments:
    numbers -- a list of numerical values

    Returns:
    The average of the numbers.
    """
    total = sum(numbers)  # Sum up all the numbers
    count = len(numbers)  # Count how many numbers there are
    if count == 0:
        return 0  # Avoid division by zero
    return total / count

# Example usage
scores = [80, 90, 75, 88]
average = calculate_average(scores)
print("The average score is:", average)
```

---

## Comments in Functions

Comments are critical for explaining the purpose of functions and their parameters.

### Single-line Comments

Use `#` for single-line comments:

```python
# This function multiplies two numbers
def multiply(a, b):
    return a * b
```

### Docstrings

Use triple quotes (`"""` or `'''`) for multi-line documentation strings:

```python
def divide(a, b):
    """
    Divides one number by another.
    Arguments:
    a -- numerator
    b -- denominator
    Returns:
    The result of division.
    """
    return a / b
```

### Multi-line Comments

For longer explanations, you can use multiple single-line comments or a multi-line string (though the latter is not
executed as a comment):

```python
"""
This function demonstrates how to use multi-line comments
to describe complex logic or behavior in your code.
"""
def example_function():
    pass
```

::: tip "Why Comment Your Code?"
Good comments clarify intent, especially in complex logic or when collaborating with others.
:::

---

## Using the `math` Module

The `math` module provides many useful mathematical functions and constants. To use it, import the module:

```python
import math

# Example: Square root and pi
print("Square root of 16:", math.sqrt(16))
print("Value of pi:", math.pi)
```

### Common `math` Functions:

- `math.sqrt(x)` — Returns the square root of `x`.
- `math.pow(x, y)` — Raises `x` to the power of `y`.
- `math.sin(x)` — Computes the sine of `x` (radians).
- `math.cos(x)` — Computes the cosine of `x` (radians).
- `math.log(x)` — Computes the natural logarithm of `x`.

::: tip "Importing Specific Functions"
You can import specific functions from the `math` module to avoid prefixing them with `math.`:

```python
from math import sqrt, pi

print("Square root of 25:", sqrt(25))
print("Value of pi:", pi)
```

:::

---

## Number Representations

Python supports multiple number formats, such as binary, hexadecimal, negative, and floating-point numbers.

### Binary Numbers

Binary numbers use base-2 and are prefixed with `0b`:

```python
binary_num = 0b1010  # Binary for 10
print("Binary number:", binary_num)
```

### Hexadecimal Numbers

Hexadecimal numbers use base-16 and are prefixed with `0x`:

```python
hex_num = 0x1A  # Hexadecimal for 26
print("Hexadecimal number:", hex_num)
```

### Negative Numbers

Negative numbers are straightforward:

```python
negative_num = -42
print("Negative number:", negative_num)
```

### Floating-Point Numbers

Floating-point numbers are used for decimal values and can represent very large or very small numbers:

```python
float_num = 3.14159
scientific_notation = 1.23e4  # Equivalent to 1.23 * 10^4
print("Floating-point number:", float_num)
print("Scientific notation:", scientific_notation)
```

::: tip "Floating-Point Precision"
Floating-point numbers are not always precise due to how they are stored in memory. Avoid comparing them directly:

```python
x = 0.1 + 0.2
print(x == 0.3)  # False due to precision issues
```

:::

---

## Using IDE Autocomplete

Modern IDEs provide autocomplete features that significantly speed up development. When you type a variable or module
name followed by a dot (`.`), a list of available attributes and methods appears.

### Example:

```python
import math

# Start typing "math." and your IDE will suggest options like sqrt, pi, log, etc.
square_root = math.sqrt(25)
print(square_root)
```

::: tip "Explore Autocomplete"
Use autocomplete to:

- Discover available methods in a module or object.
- Avoid syntax errors by selecting from valid options.
- Speed up development by reducing typing.
  :::

---

