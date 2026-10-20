// Function signature

enum ParameterValueType {
    String = 'string',
    Boolean = 'boolean',
    Integer = 'integer',
    Float = 'float',
    Array = 'array',
    Dictionary = 'dictionary',
}

type FunctionParameterDictionary = {
    type: ParameterValueType.Dictionary;
    keyType: ParameterValueType.Boolean | ParameterValueType.Integer | ParameterValueType.Float | ParameterValueType.String;
    valueType: ParameterValueType;
}

type FunctionParameterArray = {
    type: ParameterValueType.Array;
    valueType: ParameterValueType;
}

type FunctionParameterBasic = {
    type: ParameterValueType.String | ParameterValueType.Boolean | ParameterValueType.Integer | ParameterValueType.Float
}

type FunctionParameterType = FunctionParameterBasic | FunctionParameterArray | FunctionParameterDictionary;

type FunctionParameter = {
    type: FunctionParameterType;
    content: unknown;
}

type FunctionParameterWithName = FunctionParameter & {
    name: string;
}

type TestFunctionData = {
    functionName: string;
    parameters: FunctionParameterWithName[];
    return: FunctionParameter;
}

// Testing

enum TestVisibilityType {
    Public = 'public',
    Private = 'private'
}

enum AutomatedTestType {
    Standard = 'standard',
    Functional = 'functional'
}

type CodingTestBase = {
    type: AutomatedTestType;
        // weight defaults to 1 if not specified, and is used to calculate the total score of the assignment based on the sum of all test weights
    weight?: number;
}

type FunctionalTest = CodingTestBase & {
    type: AutomatedTestType.Functional;
    function: TestFunctionData;
    result: FunctionParameter;
}

type StandardTest = CodingTestBase & {
    type: AutomatedTestType.Standard;
    stdin?: string;
    stdout: string;
    stderr?: string;
    exitCode?: number;
}

// workspace
enum WorkspaceLibBundle {
    SDL3 = 'sdl3',
    SDL3_OpenGL = 'sdl3_opengl',
    CMake = 'cmake',
    Raylib = 'raylib',
    Allegro = 'allegro',
}

enum CodingLanguages {
    C = 'c',
    Cpp = 'cpp',
    Python = 'python',
    JavaScript = 'javascript',
    TypeScript = 'typescript',
    Rust = 'rust',
}

enum ToolSet {
    Clang = 'clang',
    Rustc = 'rustc',
    QuickJS = 'quickjs',
    Python = 'python',
    DotNet = 'dotnet',
}

type WorkspaceEnvironment = {
    name: string;
    language: CodingLanguages;
    tools: ToolSet;
    WorkspaceLibBundle?: WorkspaceLibBundle;
}

// type coding
type CodingAssignment = {
    version: string;
    type: 'coding';
    description: string;
    data: folder;
    environment: WorkspaceEnvironment;
    tests: {
        public: (StandardTest | FunctionalTest)[];
        private: (StandardTest | FunctionalTest)[];
    };
}

// Storage content

enum StorageContentType{
    Base64 = 'base64',
    Text = 'text',
    Folder = 'folder'
}

type file = {
    type: StorageContentType.Base64 | StorageContentType.Text;
    name: string;
    content: string;
}

type folder = {
    type: StorageContentType.Folder;
    name: string;
    folders: folder[];
    files: file[];
}

// types of workspaces

const CppTerminalEnvironment: WorkspaceEnvironment = {
    name: 'C++ Terminal',
    language: CodingLanguages.Cpp,
    tools: ToolSet.Clang
};

const CTerminalEnvironment: WorkspaceEnvironment = {
    name: 'C Terminal',
    language: CodingLanguages.C,
    tools: ToolSet.Clang
};

const Sdl3CppEnvironment: WorkspaceEnvironment = {
    name: 'SDL3 C++',
    language: CodingLanguages.Cpp,
    tools: ToolSet.Clang,
    WorkspaceLibBundle: WorkspaceLibBundle.SDL3
};

const Sdl3OpenGLCppEnvironment: WorkspaceEnvironment = {
    name: 'SDL3 OpenGL C++',
    language: CodingLanguages.Cpp,
    tools: ToolSet.Clang,
    WorkspaceLibBundle: WorkspaceLibBundle.SDL3_OpenGL
};

const RaylibCppEnvironment: WorkspaceEnvironment = {
    name: 'Raylib C++',
    language: CodingLanguages.Cpp,
    tools: ToolSet.Clang,
    WorkspaceLibBundle: WorkspaceLibBundle.Raylib
};

const RaylibCEnvironment: WorkspaceEnvironment = {
    name: 'Raylib C',
    language: CodingLanguages.C,
    tools: ToolSet.Clang,
    WorkspaceLibBundle: WorkspaceLibBundle.Raylib
};

const AllegroCppEnvironment: WorkspaceEnvironment = {
    name: 'Allegro C++',
    language: CodingLanguages.Cpp,
    tools: ToolSet.Clang,
    WorkspaceLibBundle: WorkspaceLibBundle.Allegro
};

const AllegroCEnvironment: WorkspaceEnvironment = {
    name: 'Allegro C',
    language: CodingLanguages.C,
    tools: ToolSet.Clang,
    WorkspaceLibBundle: WorkspaceLibBundle.Allegro
};