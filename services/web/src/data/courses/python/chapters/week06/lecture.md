# Loops

Loops are fundamental in Python for performing repetitive tasks. Python's for loop iterates over sequences (like lists, strings, or ranges) in a clean and readable way. In this lecture, we’ll explore several common looping techniques and dive into some advanced topics such as controlling ranges, sorting selections, nested loops, breaking out of nested loops, and common loop patterns.

<details> 

<summary>Loops Overview</summary>

- range: Iterate over a sequence of numbers with full control over start, stop, and step.
- list: Iterate directly over each element in a list.
enumerate with index: Get both the index and the element simultaneously.
sorted: Sort a list and iterate over its elements.
sorted and subset: Sort a list (e.g., of grades) and select a subset (e.g., the three lowest grades).
- Nested loops: Loop inside another loop.
- Breaking nested loops: Techniques to exit multiple loops elegantly.
- Loop Patterns: Common patterns such as accumulators, filtering, flags, and more.

</details>

## 1. For in range

The range() function generates a sequence of numbers. It can be controlled using:

- Start and Stop: range(start, stop) starts at start and stops at stop - 1.
- Step: range(start, stop, step) allows you to jump by a given amount.

### Examples:

Default Range (0 to n-1):

``` python
for i in range(5):  # Generates numbers 0, 1, 2, 3, 4
    print("Iteration:", i)
```

Custom Start and Stop:

``` python
for i in range(2, 7):  # Generates numbers 2, 3, 4, 5, 6
    print("Iteration:", i)
```

Jumping by Steps:

``` python
for i in range(1, 10, 2):  # Generates numbers 1, 3, 5, 7, 9
    print("Jumping 2 by 2:", i)
```

Counting Backwards:

``` python
for i in range(10, 0, -2):  # Generates numbers 10, 8, 6, 4, 2
    print("Counting backwards by 2:", i)
```

!!! code
{
"description": "Implement a function to print numbers from 10 to 2 in reverse order jumping by 2.",
"language": "python",
"code": "def print_reverse():\n   # your code goes here\n\n# do not modify the code below\nprint_reverse()",
"expectedOutput": "10\n8\n6\n4\n2"
}
!!!

### 2. For in list

Iterate directly over each element in a list:

``` python
fruits = ["apple", "banana", "cherry"]
for fruit in fruits:
    print(fruit)
```

- Each loop iteration gives you the next element in the list.
- This method is simple and very readable.

### 3. For in enumerate with index

When you need both the element and its index, `enumerate()` is very handy:

``` python
for index, fruit in enumerate(fruits):
    print(f"Index {index}: {fruit}")
```

- `enumerate(iterable)` returns tuples of `(index, element)`.
- You can also specify a different starting index: `enumerate(iterable, start=1)`.

!!! code
{
"description": "Implement a function to print the index and value of each element in a list in the format 'Index {index}: {value}'.",
"language": "python",
"code": "def print_index_and_value(cities):\n    # your code goes here\n\n# do not modify the code below\nprint_index_and_value(['New York', 'Los Angeles', 'Chicago'])",
"expectedOutput": "Index 0: New York\nIndex 1: Los Angeles\nIndex 2: Chicago"
}
!!!

### 4. For in sorted with ranges

You can use `sorted()` to sort a list and then select a subset. For example, suppose you have a list of grades and want to print the three lowest grades:

``` python
grades = [88, 92, 79, 85, 95, 70]
for grade in sorted(grades)[:3]: # slice the first 3 elements
    print(grade)
```

- `sorted()` returns a new sorted list without modifying the original.
- Slicing (`sorted_grades[:3]`) lets you select only the first three elements, which are the lowest in this case.

### 5. Nested Loops

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

### 6. Breaking Nested Loops

Using the `break` statement in a nested loop only exits the innermost loop. To break out of multiple loops at once, consider these approaches:

#### 6.1 Using a Flag

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

#### 6.2 Using a Function return

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

### 7. Loop Patterns

Beyond the basic loop constructs, several common patterns help you solve recurring problems more elegantly. These include accumulators, filtering, flags, and more.

#### 7.1 Accumulator Pattern

The accumulator pattern is used to combine or sum up values from a loop. This pattern is useful for aggregating data, such as summing numbers or concatenating strings.

``` python
# Example: Summing a List of Numbers
numbers = [1, 2, 3, 4, 5]
total = 0
for num in numbers:
    total += num
print("Total Sum:", total)  # Output: Total Sum: 15
```

``` python
words = ["Hello", "world", "from", "Python"]
sentence = ""  # Accumulator string
for word in words:
    sentence += word + " "
print(sentence.strip())
```

!!! code
{
"description": "Implement a function to calculate the product of all numbers in a list.",
"language": "python",
"code": "def product_of_list(numbers):\n    # your code goes here\n\n# do not modify the code below\nprint(product_of_list([1, 2, 3, 4]))",
"expectedOutput": "24"
}
!!!

#### 7.2 Filtering Pattern

The filtering pattern involves iterating over a sequence and selecting only those elements that meet a specified condition. Often, you store the filtered elements in a new list.

``` python
# Example: Filtering Even Numbers
numbers = [1, 2, 3, 4, 5, 6, 7, 8, 9, 10]
evens = []  # Container for even numbers
for number in numbers:
    if number % 2 == 0:
        evens.append(number)
print("Even numbers:", evens)
```

!!! code
{
"description": "Implement a function to filter all leap years from a list of years.",
"language": "python",
"code": "#A year is a leap year if it's evenly divisible by 4. For example, 2024 and 2028 are leap years.\nA year that's divisible by 100, but not by 400, is not a leap year. For example, 1700, 1800, and 1900 were not leap years.\nA year that's divisible by 400 is a leap year. For example, 2000 and 2400 are leap years.\n\ndef filter_leap_years(years):\n    # your code goes here\n\n# do not modify the code below\nprint(filter_leap_years([1700,1800,1900,2000, 2001, 2002, 2003, 2004, 2005, 2006, 2007, 2008]))",
"expectedOutput": "[2000, 2004, 2008]"
}
!!!

#### 7.3 Search or Flag Pattern

The flag pattern uses a boolean variable (flag) to signal when a condition has been met during iteration. This is especially useful for terminating loops early when a desired condition occurs.

``` python
# Example: Finding a Specific Element
items = ["apple", "banana", "cherry", "date"]
found_flag = False  # Flag indicating if the element is found

for item in items:
    if item == "cherry":
        found_flag = True
        break
if found_flag:
    print("Cherry was found!")
else:
    print("Cherry was not found.")
```

!!! code
{
"description": "Implement a function to check if a list contains negative numbers.",
"language": "python",
"code": "def contains_negative(numbers):\n    # your code goes here\n\n# do not modify the code below\nprint(contains_negative([1, 2, -3, 4]))",
"expectedOutput": "True"
}
!!!

#### 7.4 Counting Pattern

Count how many times a particular condition is met.

``` python
colors = ["red", "blue", "red", "green", "blue", "red"]
count_red = 0
for color in colors:
    if color == "red":
        count_red += 1
print("Number of reds:", count_red)  # Output: Number of reds: 3
```

!!! code
{
"description": "Implement a function to count the number of vowels in a string.",
"language": "python",
"code": "def count_vowels(s):\n    # your code goes here\n\n# do not modify the code below\nprint(count_vowels('Hello World'))",
"expectedOutput": "3"
}
!!!

#### 7.5 Building a Collection with transformation

Transform each element during iteration and add it to a new list.

``` python
numbers = [1, 2, 3, 4, 5]
squared = []
for number in numbers:
    squared.append(number ** 2)
print("Squared numbers:", squared)  # Output: Squared numbers: [1, 4, 9, 16, 25]
```

#### 7.6 Early Exit

Stop processing as soon as a condition is met by encapsulating your loop in a function.

``` python
def find_first_negative(numbers):
    for number in numbers:
        if number < 0:
            return number  # Immediately exits the function
    return None

nums = [1, 2, 3, -4, 5]
first_negative = find_first_negative(nums)
print("First negative number:", first_negative)
```

!!! code
{
"description": "Implement a function to find the first float number in a list.",
"language": "python",
"code": "def find_first_float(numbers):\n    # your code goes here\n\n# do not modify the code below\nprint(find_first_float([1, 2, 3.5, 4, 5]))",
"expectedOutput": "3.5"
}
!!!

#### Find the max or min value

Find the maximum or minimum value in a list.

``` python
numbers = [3, 1, 4, 1, 5, 9]
max_value = numbers[0]
for number in numbers:
    if number > max_value:
        max_value = number
print("Maximum value:", max_value)  # Output: Maximum value: 9
```

!!! code
{
"description": "Implement a function to find the minimum value in a list.",
"language": "python",
"code": "def find_min(numbers):\n    # your code goes here\n\n# do not modify the code below\nprint(find_min([3, 1, 4, 1, 5, 9]))",
"expectedOutput": "1"
}
!!!