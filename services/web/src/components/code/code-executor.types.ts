// todo: add other languages later
export type CodeLanguage = 'c' | 'cpp' | 'c++' | 'python';
// todo: add other packages later
export type WasmPackage = 'clang' | 'python';

function languageToPackage(language: CodeLanguage): WasmPackage {
  switch (language) {
    case 'c':
    case 'cpp':
    case 'c++':
      return 'clang';
    case 'python':
      return 'python';
    default:
      throw new Error(`Unsupported language ${language}`);
  }
}

export enum RunnerStatus {
  UNINITIALIZED = 'Uninitialized',
  LOADING = 'Loading',
  FAILED_LOADING = 'FailedLoading',
  READY = 'Ready',
  RUNNING = 'Running',
  FAILED_EXECUTION = 'FailedExecution',
}

export type FileMap = { [key: string]: string | Uint8Array };
export type Directory = {
  [key: string]: FileMap | Directory;
};
export type ProjectData = FileMap | Directory | string;

export type SimpleCodingOutputOnly = {
  output: string;
};

export type SimpleCodingTest = {
  input: string;
} & SimpleCodingOutputOnly;

// while testing complexity, we need to test the code with different sizes and then do some regression on the samples to infer the complexity
export type CodeComplexityTestEntry = SimpleCodingTest & {
  // used to determine the complexity in the regression algorithm
  // the first entry usually is 1 and the others should be proportional to the size of the input
  time: number;

  // the size of the input
  // probably not needed, but it could be useful to determine the complexity
  n: number;
};

export type CodeComplexityTest = {
  entries: CodeComplexityTestEntry[];

  // acceptable error variance when comparing the relative time results against the expected time
  // the lower, the more accurate the results
  tolerance: number;
};

export type CodingTestParams = {
  // it could be just a string for a file or a filesystem
  files: Directory | FileMap | string;
  tests: SimpleCodingOutputOnly | SimpleCodingTest[] | CodeComplexityTestEntry[];
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
  data: ProjectData;
  stdin?: string;
};
