# How to create courses and lectures

Our CMS is work in progress, but the markdown renderer is working fine. We still need to implement the backend and the
frontend to create and edit courses and lectures.

## Folder and file structure

For now, you can create courses and lectures by following the steps below:

- Clone the repository;
- Edit the file `services/web/src/data/courses/courses.ts`;
- Create a folder as the same slug of your course in `services/web/src/data/courses/`;
- Create a file `service/web/src/data/courses/<course-slug>/index.ts` with the content of your course. If you are in
  doubt, you can copy the structure of the other courses;
- Create a syllabus for the course in the file `services/web/src/data/courses/<course-slug>/syllabus.md`, this would be
  the content of the course page;
- Create a file `services/web/src/data/courses/<course-slug>/chapters/<chapter-slug>/index.ts` as the index of your
  chapter. If in doubt, copy the structure of the other chapters;
- Create md files for the contents and index them on the index.ts.
- Commit and push your changes to the server.

### In summary:

- `services/web/src/data/courses/` modify the index.ts to include your course;
- `services/web/src/data/courses/<course-slug>/index.ts` to index the chapters and syllabus;
- `services/web/src/data/courses/<course-slug>/syllabus.md` for the course description;
- `services/web/src/data/courses/<course-slug>/chapters/<chapter-slug>/index.ts` to index the lectures;
- `services/web/src/data/courses/<course-slug>/chapters/<chapter-slug>/<filename>.md` for the lecture contents.

## Markdown structure

You can follow the common structure of the markdown files, check
the [markdown syntax](https://docs.github.com/en/get-started/writing-on-github/getting-started-with-writing-and-formatting-on-github/basic-writing-and-formatting-syntax).
But customize it with some additions:

### Quiz

To add a quiz, you can use the following structure:

!!! quiz
{
"title": "Basic `if-else` Quiz",
"question": "What will be printed when `x = 3` in the following code?\n\n
```python\nif x > 5:\n    print(\"Greater than 5\")\nelse:\n    print(\"5 or less\")\n```",
"options": ["Greater than 5", "5 or less", "No output", "Error"],
"answers": ["5 or less"]
}
!!!

### Mermaid

To add a mermaid diagram, you can use the following structure:

``` mermaid
flowchart TD
    A[Christmas] -->|Get money| B(Go shopping)
    B --> C{Let me think}
    C -->|One| D[Laptop]
    C -->|Two| E[iPhone]
    C -->|Three| F[Car]
```

### Code

To add a code block, you can use the following structure:

``` python
def is_odd(n):
    return n % 2 != 0
```

### Admonitions

To add an admonition, you can use the following structure:

::: type "optional title"
body
:::

where type can be `tip`, `note`, `warning`, `important`, `caution`, `danger`, `failure`, `bug`, `example`, `quote`.
