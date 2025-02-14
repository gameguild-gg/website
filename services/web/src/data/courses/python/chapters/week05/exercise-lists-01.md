# Exercise: Two Sum

::: tip

This is the most asked question in coding interviews!

:::

Your task is to implement a function that takes a list of integers and a target integer, and returns the indices of the
two numbers such that they add up to the target.

- There is exactly one solution;
- Don't the same element twice;
- You should return the indices in ascending order.

::: example

- Input: `nums = [2, 7, 11, 15]`, `target = 9`
- Output: `[0, 1]`
- Explanation: `nums[0] + nums[1] = 2 + 7 = 9`

:::

::: warning "Challenge"

- The naive solution is O(n^2) time complexity.
- Can you make it run in O(n) time?
- What is the fastest solution without using extra space?

:::

!!! code
{
"description": "Implement a function to find the two sum.",
"language": "python",
"code": "def two_sum(nums, target):\n    pass # your code here\n\n# do not modify the code below\nprint(
two_sum([2, 7, 11, 15],
9))",
"expectedOutput": "[0, 1]"
}
!!!