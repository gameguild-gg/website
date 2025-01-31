import type { CodeQuestionv1_0_0 } from "@/lib/interface-base/question.base.v1.0.0"

export const DEFAULT_SANDBOX_QUESTION: CodeQuestionv1_0_0 = {
  id: 0,
  format_ver: "1.0.0",
  type: "code",
  rules: ["inputs: y", "outputs: y", "submit: n"],
  status: 1,
  subject: ["sandbox"],
  score: [0, 0],
  title: "Sandbox",
  description: "Welcome to the Coding Sandbox! This environment allows you to experiment with code freely. Here's how to use it:\n\n1. Write your code in the editor.\n2. Use the 'Run' button to execute your code.\n3. View the output in the console below.\n4. You can create, rename, or delete files as needed.\n5. Experiment with different programming languages.\n\nFeel free to test ideas, practice coding, or work on small projects. Happy coding!",
  initialCode: {},
  codeName: [],
  inputs: {},
  outputs: {},
  outputsScore: {},
  testResults: undefined,
  output: undefined,
  submittedCode: undefined
}

