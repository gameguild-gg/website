// todo: add other languages later
export type CodeLanguage = 'cpp' | 'python';

export enum RunnerStatus {
  UNINITIALIZED = 'Uninitialized',
  LOADING = 'Loading',
  FAILED_LOADING = 'FailedLoading',
  READY = 'Ready',
  RUNNING = 'Running',
  FAILED_EXECUTION = 'FailedExecution',
}

// the key can be the directory name or the file name
// the value can be a string or a Uint8Array
export type FileMap = { [key: string]: string | Uint8Array | FileMap };

/* simple stdin stdout test */
export type SimpleCodingOutputOnly = {
  stdout: string;
};
export type SimpleCodingTest = {
  stdin: string;
} & SimpleCodingOutputOnly;

/* function testing: args / result test */
export type FunctionCodingTest = {
  // the arguments to be passed to the function
  args: any[];

  // the expected output of the function
  expectedReturn: any;
};
export type FunctionCodingTests = {
  publicTests: FunctionCodingTest[];
  hiddenTests: FunctionCodingTest[];
};

/* the instructor can define their own tests */
export type InstructorDefinedTest = {
  // the source of the tester, implemented by the instructor
  // useful for testing time complexity
  // the instructor can implement their own solution for benchmarking and then compare the time against the user's solution
  testerSourceCode: string;
};
export type InstructorDefinedTestResultEntry = {
  // number between 0 and 1 for each individual test
  grade: number;
  // if the test pass, there is no need to show message
  message?: string;
};
export type InstructorDefinedTestResult = {
  // number between 0 and 1
  grade: number;
  // verbose message to be shown to the user
  results?: InstructorDefinedTestResultEntry[];
};
export type InstructorDefinedTests = {
  publicTests: InstructorDefinedTest[];
  hiddenTests: InstructorDefinedTest[];
};

export type CodingTestParams = {
  // it could be just a string for a file or a filesystem
  files: Directory | FileMap | string;
  tests: SimpleCodingOutputOnly | SimpleCodingTest[] | InstructorDefinedTests[];
};

export enum CodeComplexity {
  O1 = 'O(1)',
  OlogN = 'O(log n)',
  Olinear = 'O(n)',
  OlinearLogN = 'O(n log n)',
  Oquadratic = 'O(n^2)',
  Ocubic = 'O(n^3)',
  Oexponential = 'O(2^n)',
  Ofactorial = 'O(n!)',
}

export type CompileAndRunParams = {
  language: CodeLanguage;
  data: FileMap;
  stdin?: string;
  packages?: string[];
};

export type CompileAndRunResult = {
  stdout: string;
  duration: number;
};
