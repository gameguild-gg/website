import {AssignmentBasev1_0_0} from "../assignment.base.v1.0.0";

export const HelloWorldCPP: AssignmentBasev1_0_0 = {
    format_ver: "1.0.0",
    description: `# Hello World
## Context
In conputer science, "Hello World" is the traditional first program that is written when learning a new programming language. It is a simple program that outputs "Hello World" to the console.

## Concept
A basic program in C++, consists in two parts: Preprocessor Directives and Main Function. 

For this first program, we will the following Preprocessor Directives:

\`\`\`cpp
#include <iostream> // this includes the console input/output stream library
using namespace std; // this allows us to use the standard library without having to specify it every time
\`\`\`

The main function is the entry point for all C++ executables. You can write it as follows:

\`\`\`cpp
// \`int\` means that the function returns an integer to the operating system
// \`main\` is the name of the function, by default the program with a function called \`main\`.
int main() { 
    // Your code goes here
    return 0;
}
\`\`\`

To print one line to the console, you can use the \`cout\` object from the standard library. It is used as follows:

\`\`\`cpp
cout << "Your message goes here" << endl;
\`\`\`

## Task 
Write a program that prints "Hello World!" to the console and then jump to the next line.
    `,
    initialCode: `#include <iostream>
using namespace std;

int main() {
    // Your code goes here
    return 0;
}`,
    inputs: [""],
    outputs: ["Hello World!"]
}