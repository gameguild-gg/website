# Sets in Python

Sets are unordered collections of unique elements in Python. They're useful for removing duplicates and performing mathematical set operations.

## Set Syntax

Creating a set is straightforward:

``` python
# Empty set (note: {} creates a dictionary, not a set)
empty_set = set()

# Set with initial values
fruits = {"apple", "banana", "orange"}

# Creating a set from another iterable
numbers = set([1, 2, 3, 3, 2, 1])  # Will contain {1, 2, 3}
```

## Sets vs Lists vs Dictionaries

::: note "Key Differences"
Sets, lists, and dictionaries serve different purposes:
:::

| Sets | Lists | Dictionaries            |
|------|-------|-------------------------|
| Unordered collection | Ordered collection | Key-value pairs         |
| No duplicates | Can contain duplicates | Unique keys with values |
| No indexing | Access by index | Access by key           |
| Fast membership testing | Slower membership testing | Fast key lookup         |
| Mutable | Mutable | Mutable                 |

## Basic Set Methods

### Adding and Removing Elements

``` python
fruits = {"apple", "banana", "orange"}

# Adding elements
fruits.add("mango")

# Remove an element
fruits.remove("banana")  # Raises KeyError if not found

# Safe removal
fruits.discard("grape")  # No error if not found

# Remove and return an arbitrary element
item = fruits.pop()

# Clear all elements
fruits.clear()  # results in set()
```

### Set Operations

``` python
set_a = {1, 2, 3, 4, 5}
set_b = {4, 5, 6, 7, 8}

# Union (all elements from both sets)
print(set_a | set_b)  # {1, 2, 3, 4, 5, 6, 7, 8}
print(set_a.union(set_b))  # Same result

# Intersection (elements in both sets)
print(set_a & set_b)  # {4, 5}
print(set_a.intersection(set_b))  # Same result

# Difference (elements in set_a but not in set_b)
print(set_a - set_b)  # {1, 2, 3}
print(set_a.difference(set_b))  # Same result

# Symmetric difference (elements in either set, but not both)
print(set_a ^ set_b)  # {1, 2, 3, 6, 7, 8}
print(set_a.symmetric_difference(set_b))  # Same result
```

## Checking Membership

Sets provide fast membership testing:

``` python
fruits = {"apple", "banana", "orange"}

# Check if an element exists
if "banana" in fruits:
    print("Yes, banana is in the set")
    
# Check if an element is not in the set
if "grape" not in fruits:
    print("No, grape is not in the set")
```

::: warning "Mutable Elements"
Sets can only contain hashable (immutable) objects. Lists, dictionaries, and other sets cannot be elements of a set.
:::

## Common Use Cases

::: tip "Common Applications"
Sets are ideal for:
- Removing duplicates from sequences
- Membership testing
- Mathematical set operations
- Finding unique elements
  :::

!!! code
{
"description": "Implement a function to find and return only unique elements from a list.",
"language": "python",
"code": "def get_unique_elements(items):\n    # your code goes here\n\n# do not modify the code below\nprint(get_unique_elements([1, 2, 3, 1, 2, 4, 5]))",
"expectedOutput": "{1, 2, 3, 4, 5}"
}
!!!

!!! code
{
"description": "Create a function that takes two lists and returns elements that appear in either list but not both.",
"language": "python",
"code": "def symmetric_difference(list1, list2):\n    # your code goes here\n\n# do not modify the code below\nprint(symmetric_difference([1, 2, 3, 4], [3, 4, 5, 6]))",
"expectedOutput": "{1, 2, 5, 6}"
}
!!!

!!! code
{
"description": "Write a function to check if one set is a subset of another set.",
"language": "python",
"code": "def is_subset(set1, set2):\n    # your code goes here\n\n# do not modify the code below\nprint(is_subset({1, 2}, {1, 2, 3, 4}))\nprint(is_subset({1, 5}, {1, 2, 3, 4}))",
"expectedOutput": "True\nFalse"
}
!!!

::: note "Set Comprehensions"
Like lists and dictionaries, Python supports set comprehensions:

``` python
# Create a set of squares of even numbers from 0 to 9
even_squares = {x*x for x in range(10) if x % 2 == 0}
# Result: {0, 4, 16, 36, 64}
```
:::
