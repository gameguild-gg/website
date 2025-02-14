# Exercise: Search Insert Position

Your task is to implement a function that searches for a target value in a sorted list and returns the index if found.
If not found, it should return the index where it would be if it were inserted in order.

::: example

- Input: `nums = [1, 3, 5, 6]`, `target = 5`
- Output: `2`
- Explanation: `5` is found at index `2`.

:::

::: example

- Input: `nums = [1, 3, 5, 6]`, `target = 2`
- Output: `1`
- Explanation: `2` is not found, but would be inserted at index `1`.

:::

::: example

- Input: `nums = [1,3,5,6]`, `target = 7`
- Output: `4`
- Explanation: `7` is not found, but would be inserted at index `4`.

:::

::: danger "Challenge"

- Can you make it make it run fast? Like O(log n)?

:::

!!! code
{
"description": "Implement a function to find the search insert position.",
"language": "python",
"code": "def search_insert(nums, target):\n # your code here\n\n# do not modify the code below\nprint(
search_insert([1, 3, 5, 6], 5))\nprint(search_insert([1, 3, 5, 6], 2))\nprint(search_insert([1, 3, 5, 6], 7))",
"expectedOutput": "2\n1\n4"
}

!!!
