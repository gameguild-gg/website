# Dictionary

Dictionaries are one of Python's most powerful and flexible data structures. They allow you to store data as key-value pairs, providing fast lookups regardless of size.


![Dictionaries](https://preview.redd.it/python-meme-true-v0-g6vhhhsht6b91.jpg?auto=webp&s=21c5fc970b99544bdb2d60b760b112560709030a)


## Dictionary Syntax

Creating a dictionary is simple:

```python
# Empty dictionary
empty_dict = {}

# Dictionary with initial key-value pairs
student = {
    "name": "John",
    "age": 21,
    "courses": ["Math", "Computer Science"],
    "graduated": False
}

# Alternative creation using dict()
another_dict = dict(name="Jane", age=22)
```

## Lists vs Dictionaries

::: note "Key Differences"
Lists and dictionaries serve different purposes and have fundamental differences:
:::

| Lists | Dictionaries |
|-------|-------------|
| Ordered collection | Unordered collection of key-value pairs |
| Accessed by index (0, 1, 2...) | Accessed by keys (which can be strings, numbers, etc.) |
| Use `my_list[0]` to access elements | Use `my_dict["key"]` to access elements |
| Useful for sequential data | Useful for relational data or fast lookups |

## Basic Dictionary Methods

### Accessing Keys, Values and Items

```python
student = {"name": "John", "age": 21, "major": "Computer Science"}

# Get all keys
print(student.keys())  # dict_keys(['name', 'age', 'major'])

# Get all values
print(student.values())  # dict_values(['John', 21, 'Computer Science'])

# Get all key-value pairs as tuples
print(student.items())  # dict_items([('name', 'John'), ('age', 21), ('major', 'Computer Science')])
```

### Using the get() Method

The `get()` method safely retrieves values without raising errors for missing keys:

```python
student = {"name": "John", "age": 21}

# Standard access (raises KeyError if key doesn't exist)
# print(student["major"])  # KeyError: 'major'

# Safe access with get() - returns None if key doesn't exist
print(student.get("major"))  # None

# Provide a default value if key doesn't exist
print(student.get("major", "Not specified"))  # Not specified
```

::: tip "Best Practice"
Always use `get()` when you're not certain if a key exists to avoid KeyError exceptions.
:::

## Checking if Keys Exist

There are several ways to check if a key exists in a dictionary:

```python
student = {"name": "John", "age": 21}

# Using the 'in' operator
if "age" in student:
    print("Age is present")

# Using get() method with a default value
if student.get("major") is not None:
    print("Major is present")
else:
    print("Major is not present")
```

## Modifying Dictionaries

```python
student = {"name": "John", "age": 21}

# Adding or updating values
student["major"] = "Computer Science"
student["age"] = 22

# Removing items
del student["age"]

# Pop method removes and returns the value
major = student.pop("major")
print(major)  # Computer Science

# Clear all items
student.clear()  # {}
```

::: warning "Mutable Keys"
Dictionary keys must be immutable (strings, numbers, tuples). Lists, dictionaries, and sets cannot be used as keys because they can change.
:::

## Practical Applications

Dictionaries are excellent for:
- Configuration settings
- Caching results
- Counting occurrences
- Converting between data formats

!!! code
{
"description": "Implement a function that counts the frequency of each word in a string and returns a dictionary with words as keys and their counts as values.",
"language": "python",
"code": "def count_words(text):\n    # your code goes here\n\n# do not modify the code below\nprint(count_words(\"apple banana apple orange banana apple\"))",
"expectedOutput": "{'apple': 3, 'banana': 2, 'orange': 1}"
}
!!!

!!! code
{
"description": "Create a function that takes a list of dictionaries (each representing a student with 'name' and 'grade' keys) and returns the name of the student with the highest grade.",
"language": "python",
"code": "def find_top_student(students):\n    # your code goes here\n\n# do not modify the code below\nstudents = [\n    {\"name\": \"Alice\", \"grade\": 85},\n    {\"name\": \"Bob\", \"grade\": 92},\n    {\"name\": \"Charlie\", \"grade\": 78}\n]\nprint(find_top_student(students))",
"expectedOutput": "Bob"
}
!!!

!!! code
{
"description": "Write a function that merges two dictionaries. If there are duplicate keys, the values from the second dictionary should take precedence.",
"language": "python",
"code": "def merge_dictionaries(dict1, dict2):\n    # your code goes here\n\n# do not modify the code below\nd1 = {\"a\": 1, \"b\": 2, \"c\": 3}\nd2 = {\"b\": 4, \"d\": 5}\nprint(merge_dictionaries(d1, d2))",
"expectedOutput": "{'a': 1, 'b': 4, 'c': 3, 'd': 5}"
}
!!!

::: note "Dictionary Comprehensions"
Just like list comprehensions, Python supports dictionary comprehensions:

```python
# Create a dictionary of squares
squares = {x: x*x for x in range(1, 6)}
# Result: {1: 1, 2: 4, 3: 9, 4: 16, 5: 25}
```
:::