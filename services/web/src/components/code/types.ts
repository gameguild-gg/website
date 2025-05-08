// todo: add other languages later
export type CodeLanguage = 'c' | 'cpp' | 'python' | 'javascript' | 'typescript' | 'rust' | 'c#' | 'lua';

export type SupportedExtensionsType = Record<CodeLanguage, string[]>;

export const SupportedExtensionsByEachLanguage: SupportedExtensionsType = {
  c: ['.c', '.h'],
  cpp: ['.cpp', '.cxx', '.cc', '.h', '.hpp'],
  python: ['.py'],
  javascript: ['.js', '.css', '.html'],
  rust: ['.rs'],
  'c#': ['.cs'],
  lua: ['.lua'],
};

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
export type SimpleCodingTest = {
  stdout: string;
  stdin?: string;
};
export type SimpleCodingTests = {
  publicTests: SimpleCodingTest[];
  hiddenTests: SimpleCodingTest[];
};

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
export type InstructorDefinedTestResult = {
  // number between 0 and 1 for each individual test
  grade: number;
  // if the test pass, there is no need to show message
  message?: string;
};
export type InstructorDefinedTests = {
  publicTests: InstructorDefinedTest[];
  hiddenTests: InstructorDefinedTest[];
};

// CodingTestParams is bounded to a language
export type CodingTestParams = {
  // it could be just a string for a file or a filesystem
  boilerplate: FileMap | string; // boilerplate code
  tests?: SimpleCodingTests | InstructorDefinedTests | FunctionCodingTests; // tests to be run
};

export type CodeChallenge = Record<CodeLanguage, CodingTestParams>;

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
