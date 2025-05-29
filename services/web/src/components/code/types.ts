

export class SupportedExtensionEntry {
  language: CodeLanguage;
  extensions: string[];
}

export const SupportedExtensionsByEachLanguage: SupportedExtensionEntry[] = [
  {
    language: CodeLanguage.c,
    extensions: ['.c', '.h'],
  },
  {
    language: CodeLanguage.cpp,
    extensions: ['.cpp', '.cxx', '.cc', '.h', '.hpp'],
  },
  {
    language: CodeLanguage.python,
    extensions: ['.py'],
  },
  {
    language: CodeLanguage.javascript,
    extensions: ['.js', '.css', '.html'],
  },
  {
    language: CodeLanguage.rust,
    extensions: ['.rs'],
  },
  {
    language: CodeLanguage.lua,
    extensions: ['.lua'],
  },
  {
    language: CodeLanguage['c#'],
    extensions: ['.cs'],
  },
];

// the key can be the directory name or the file name
// the value can be a string or a Uint8Array
export type FileMap = { [key: string]: string | FileMap };

// for simple interactions
export class CompileAndRunParams {
  @ApiProperty({ enum: CodeLanguage })
  language: CodeLanguage;

  @ApiProperty({
    type: 'object',
    additionalProperties: {
      oneOf: [
        { type: 'string', description: 'File content as string' },
        {
          type: 'object',
          description: 'Nested directory or file map',
          additionalProperties: { $ref: '#/components/schemas/FileMap' },
        },
      ],
    },
    description: 'A recursive map of file/directory names to file contents or nested directories.',
  } as ApiPropertyOptions)
  data: FileMap;
  stdin?: string;
  argv?: string[];
}

export class RunResult = {
  success: boolean;
  output: string;
  error: string | null;
};

export enum RunnerStatus {
  UNINITIALIZED = 'Uninitialized',
  LOADING = 'Loading',
  FAILED_LOADING = 'FailedLoading',
  READY = 'Ready',
  RUNNING = 'Running',
  FAILED_EXECUTION = 'FailedExecution',
}

export enum CodingTestEnum {
  CONSOLE = 'console',
  FUNCTION = 'function',
  CUSTOM = 'custom',
}

type CodingTestTypeConsole = {
  type: CodingTestEnum.CONSOLE;
};

type CodingTestTypeFunction = {
  type: CodingTestEnum.FUNCTION;
};

type CodingTestTypeCustom = {
  type: CodingTestEnum.CUSTOM;
};

/* simple stdin stdout test */
export type SimpleCodingTest = {
  stdout: string;
  stdin?: string;
};
export type SimpleCodingTests = {
  publicTests: SimpleCodingTest[];
  hiddenTests: SimpleCodingTest[];
} & CodingTestTypeConsole;

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
} & CodingTestTypeFunction;

/* the instructor can define their own tests */
export type InstructorDefinedTest = {
  // the source of the tester, implemented by the instructor
  // useful for testing time complexity
  // the instructor can implement their own solution for benchmarking and then compare the time against the user's solution
  testerSourceCode: string | FileMap;
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
} & CodingTestTypeCustom;

// CodingTestParams is bounded to a language
export type CodingTestParams = {
  files: FileMap | string;
  tests?: SimpleCodingTests | InstructorDefinedTests | FunctionCodingTests; // tests to be run
};

export type CodingTestParamsWithLanguage = {
  language: CodeLanguage;
} & CodingTestParams;

export type CodeChallenge = Record<CodeLanguage, CodingTestParams>;
