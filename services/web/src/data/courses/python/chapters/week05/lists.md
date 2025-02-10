# Lists

Lists in python are used to store multiple items in a single variable. Lists are created using square brackets `[]` and
items are separated by commas `,`.

``` python
# Creating a list
fruits = ["apple", "banana", "cherry"]

print(fruits)  # Output: ['apple', 'banana', 'cherry']
```

## Access Items

You can access items in a list by referring to the index number. Indexes start at `0`. So the first item has an index of
`0`, the second item has an index of `1`, and so on.

In a list of 3 elements, the indexes will be `0`, `1`, and `2`.

``` python
# Accessing items
cars = ["Ford", "Volvo", "BMW"]
print(cars[1]) # Output: Volvo
```

You can access items in reverse order by using negative indexes. The last item has an index of `-1`, the second to last
has an index of `-2`, and so on.

``` python
# Accessing items in reverse order
countries = ["Brazil", "USA", "Mexico", "Canada"]
print(countries[-1]) # Output: Canada
```

But you need to be careful when using indexes that are out of range. If you try to access an index that doesn't exist,
you will get an `IndexError`. Indexes could go from `-3` to `2` in a list of 3 elements. And the general formula is
`-(len(list))` to `len(list) - 1`.

``` python
# Accessing items with out of range index
names = ["alex", "bob", "charlie"]
print(names[3])  # IndexError: list index out of range
```

You can modify the content of a list by assigning a new value to an index.

``` python
# Modifying items
names = ["alex", "bob", "charlie"]
names[1] = "david"
print(names)  # Output: ['alex', 'david', 'charlie']
```

## Loop Through a List

You can loop through the items in a list using a `for` loop.

``` python
# Looping through a list
equipments = ["keyboard", "mouse", "monitor"]
for equipment in equipments:
    print(equipment) # will print item in a new line
```

## Check if Item Exists

You can check if an item exists in a list by using the `in` keyword.

``` python
# Checking if an item exists
sports = ["football", "basketball", "tennis"]
if "basketball" in sports:
    print("Yes, basketball is in the list")
```

## Slicing

In some cases, you will need to slice and get sublists from a list. You can do this by specifying a range of indexes.

``` python
# Slicing a list
animals = ["dog", "cat", "elephant", "giraffe", "lion"]
print(animals[1:4])  # Output: ['cat', 'elephant', 'giraffe']
print(animals[:3])  # Output: ['dog', 'cat
print(animals[2:])  # Output: ['elephant', 'giraffe', 'lion']
print(animals[-3:-1])  # Output: ['elephant', 'giraffe']
```

## Concatenation

You can concatenate two lists using the `+` operator.

``` python
# Concatenating lists
list1 = ["a", "b", "c"]
list2 = [1, 2, 3]
list3 = list1 + list2
print(list3)  # Output: ['a', 'b', 'c', 1, 2, 3]
```

## Deleting Items

You can delete items from a list using the `del` keyword.

``` python
# Deleting items
colors = ["red", "blue", "green"]
del colors[1]
print(colors)  # Output: ['red', 'green']
```

## Length of a List

You can get the number of items in a list using the `len()` function.

``` python
# Length of a list
sizes = ["large", "medium", "small"]
print(len(sizes))  # Output: 3
```

## Tuples

Tuples are similar to lists, but they are immutable. This means that once you create a tuple, you cannot change its
contents. You can create a tuple by placing a comma-separated sequence of items inside parentheses `()`. For example:

```python
# Creating a tuple
fruits = ("apple", "banana", "cherry")
fruits[0] = "orange"  # TypeError: 'tuple' object does not support item assignment
```

## Strings as lists

Strings are a sequence of characters. You can create a string by enclosing characters in single `'` or double `"`
quotes.

Python strings are constant, meaning that the characters in a string cannot be changed. But you can access elements of a
string using indexes as the same way you access items in a list.

``` python
phrase = "Hello, World!"
print(phrase[1])  # Output: e
phrase[1] = "a"  # TypeError: 'str' object does not support item assignment
```

## String and List Methods

Python has a set of built-in methods that you can use on lists and strings. Please explore
the [full list of sting methods](https://www.w3schools.com/python/python_ref_string.asp) and a list
of [array/list methods](https://www.w3schools.com/python/python_ref_list.asp)

