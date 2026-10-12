export type SupportedLanguage = 
  | "javascript" 
  | "typescript" 
  | "python" 
  | "lua" 
  | "c" 
  | "cpp"
  | "html"
  | "css"
  | "markdown"
  | "java"
  | "go"
  | "rust"
  | "php"
  | "ruby"
  | "swift"
  | "kotlin"
  | "csharp"
  | "sql"
  | "bash"
  | "powershell"
  | "r"
  | "scala"
  | "dart"
  | "json"
  | "yaml"
  | "xml"
  | "gdscript"
  | "lexical"
  | "prolog"
  | "pascal"
  | "fsharp"
  | "haskell"
  | "perl"
  | "groovy"
  | "elixir"
  | "clojure"
  | "erlang"
  | "fortran"
  | "ada"
  | "cobol"
  | "assembly_x86"
  | "assembly_arm"
  | "assembly_mips"
  | "assembly_riscv"
  | "assembly_powerpc"
  | "webassembly"
  | "forth"
  | "dlang"
  | "nim"
  | "zig"
  | "v"
  | "crystal"
  | "ocaml"
  | "scheme"
  | "smalltalk"
  | "julia"
  | "vb"
  | "objectpascal"
  | "hope"
  | "b"
  | "bcpl"
  | "fantom"
  | "modula3"
  | "fstar"
  | "elm"
  | "haxe"
  | "gleam"
  | "rescript"
  | "csharp_script"
  | "fsharp_script"
  | "assemblyscript"
  | "factor"
  | "purescript"

export type EditorMode = 
  | "execution"  // Executa código com console na direita
  | "test"       // Modo de testes

export type { ShikiTheme } from "@/components/block-content-editor/lib/shiki/themes"
export { SHIKI_THEME_CONFIGS, getShikiThemeName } from "@/components/block-content-editor/lib/shiki/themes"

export type FileType = 'f' | 'm' | 't' // f = arquivo padrão, m = main (entry point), t = test main

export interface CodeFile {
  id: string
  name: string
  content: string
  language: SupportedLanguage
  isFile: FileType // Tipo de arquivo: 'f' (padrão), 'm' (main), 't' (test)
  isVisible: boolean
  readonly?: boolean // Se true, o arquivo não pode ser editado
  path: string // Caminho completo incluindo pastas, ex: "src/components/Button.tsx"
  assetId?: string // Stable AssetUri for tracking and copy-on-write.
  isModified?: boolean // Flag para indicar se arquivo de asset foi modificado
}

export interface FileTreeFolder {
  id: string
  name: string
  path: string
  isExpanded: boolean
  isVisible: boolean
  readonly?: boolean // Se true, todos os arquivos dentro não podem ser editados
  isFocusFolder?: boolean // Se true, esta pasta é usada como pasta índice padrão para focus-editor com editorInstance="multiple"
  children: (CodeFile | FileTreeFolder)[]
  type: "folder"
}

export type FileTreeItem = CodeFile | FileTreeFolder

export type PanelType = "explorer" | "full-editor" | "focus-editor" | "output"

export type EditorInstance = "multiple" | "unique"

/**
 * Splitter-pane tree node. A display's layout is a recursive tree of either
 * leaf panels (rendered content) or splits (PanelGroup with N children and
 * percent-based sizes that sum to 100).
 *
 * direction "horizontal" lays children side-by-side (left → right).
 * direction "vertical"  stacks children top → bottom.
 */
export interface LeafPanel {
  kind: "leaf"
  id: string
  type: PanelType
  editorInstance?: EditorInstance // for "full-editor" / "focus-editor"
}

export interface SplitNode {
  kind: "split"
  id: string
  direction: "horizontal" | "vertical"
  sizes: number[] // percentages, length === children.length, must sum to ~100
  children: LayoutNode[]
}

export type LayoutNode = LeafPanel | SplitNode

export interface DisplayConfig {
  id: string // "display-1", "display-2", ...
  name: string // user-editable label
  templateId?: string // id of the template that originally seeded this display
  root: LayoutNode // splitter tree
  uniqueOpenTabs?: string[] // open tabs scoped to this display (unique editor instance)
  uniqueActiveFileId?: string // active file scoped to this display (unique editor instance)
}

export interface LayoutConfig {
  displays: DisplayConfig[]
  activeDisplayId: string
  editMode: boolean
}

export interface TestCase {
  id: string
  name: string
  type: "simple" | "function" | "custom"
  input?: string
  expectedOutput?: string
  args?: unknown[]
  expectedReturn?: any
  customCode?: string
  enabled: boolean
}

export interface TestResult {
  testId: string
  passed: boolean
  actual: string
  expected: string
  error?: string
}

export type CodeStudioMode = "execution" | "test"

export interface CodeStudioData {
  id: string // ID único do node code-studio
  files: CodeFile[]
  folders: FileTreeFolder[]
  openTabs: string[] // IDs dos arquivos abertos em abas
  activeFileId?: string
  mode: CodeStudioMode
  language: SupportedLanguage
  title?: string
  caption?: string
  
  // Configurações de UI
  readonly?: boolean
  showLineNumbers?: boolean
  fontSize?: number
  theme?: "light" | "dark" | "system"

  // Layout customizável
  layout?: LayoutConfig
  
  // Configurações de execução
  clearOnRun?: boolean
  autoRun?: boolean
  
  // Testes
  testCases?: Record<string, TestCase[]>
  showTests?: boolean
}

export interface ModeConfig {
  id: CodeStudioMode
  label: string
  description: string
  icon: string
  supportedLanguages: SupportedLanguage[]
}

export const MODE_CONFIGS: Record<CodeStudioMode, ModeConfig> = {
  execution: {
    id: "execution",
    label: "Execution Mode",
    description: "Write and execute code with real-time output console. Perfect for development and debugging.",
    icon: "Play",
    supportedLanguages: ["javascript", "typescript", "python", "lua", "c", "cpp", "php", "sql", "ruby", "webassembly"],
  },
  test: {
    id: "test",
    label: "Test Mode",
    description: "Run automated test cases against your code. Validate inputs, outputs, and expected behaviors.",
    icon: "TestTube",
    supportedLanguages: ["javascript", "typescript", "python", "lua", "c", "cpp", "php", "sql", "ruby", "webassembly"],
  },
}

export const LANGUAGE_CONFIGS: Record<
  SupportedLanguage,
  {
    label: string
    monacoLanguage: string
    defaultExtension: string
    supportsExecution: boolean
    defaultTemplate: string
  }
> = {
  javascript: {
    label: "JavaScript",
    monacoLanguage: "javascript",
    defaultExtension: ".js",
    supportsExecution: true,
    defaultTemplate: '// Write your JavaScript code here\nconsole.log("Hello, World!");',
  },
  typescript: {
    label: "TypeScript",
    monacoLanguage: "typescript",
    defaultExtension: ".ts",
    supportsExecution: true,
    defaultTemplate: '// Write your TypeScript code here\nconsole.log("Hello, World!");',
  },
  python: {
    label: "Python",
    monacoLanguage: "python",
    defaultExtension: ".py",
    supportsExecution: true,
    defaultTemplate: '# Write your Python code here\nprint("Hello, World!")',
  },
  lua: {
    label: "Lua",
    monacoLanguage: "lua",
    defaultExtension: ".lua",
    supportsExecution: true,
    defaultTemplate: '-- Write your Lua code here\nprint("Hello, World!")',
  },
  c: {
    label: "C",
    monacoLanguage: "c",
    defaultExtension: ".c",
    supportsExecution: true,
    defaultTemplate: '#include <stdio.h>\n\nint main() {\n    printf("Hello, World!\\n");\n    return 0;\n}',
  },
  cpp: {
    label: "C++",
    monacoLanguage: "cpp",
    defaultExtension: ".cpp",
    supportsExecution: true,
    defaultTemplate: '#include <iostream>\n\nint main() {\n    std::cout << "Hello, World!" << std::endl;\n    return 0;\n}',
  },
  html: {
    label: "HTML",
    monacoLanguage: "html",
    defaultExtension: ".html",
    supportsExecution: false,
    defaultTemplate: '<!DOCTYPE html>\n<html>\n<head>\n    <title>Page Title</title>\n</head>\n<body>\n    <h1>Hello, World!</h1>\n</body>\n</html>',
  },
  css: {
    label: "CSS",
    monacoLanguage: "css",
    defaultExtension: ".css",
    supportsExecution: false,
    defaultTemplate: 'body {\n    font-family: sans-serif;\n    margin: 20px;\n}\n\nh1 {\n    color: #333;\n}',
  },
  markdown: {
    label: "Markdown",
    monacoLanguage: "markdown",
    defaultExtension: ".md",
    supportsExecution: false,
    defaultTemplate: '# Hello, World!\n\nThis is a **markdown** document.\n\n- Item 1\n- Item 2\n- Item 3',
  },
  java: {
    label: "Java",
    monacoLanguage: "java",
    defaultExtension: ".java",
    supportsExecution: false,
    defaultTemplate: 'public class Main {\n    public static void main(String[] args) {\n        System.out.println("Hello, World!");\n    }\n}',
  },
  go: {
    label: "Go",
    monacoLanguage: "go",
    defaultExtension: ".go",
    supportsExecution: false,
    defaultTemplate: 'package main\n\nimport "fmt"\n\nfunc main() {\n    fmt.Println("Hello, World!")\n}',
  },
  rust: {
    label: "Rust",
    monacoLanguage: "rust",
    defaultExtension: ".rs",
    supportsExecution: true,
    defaultTemplate: 'fn main() {\n    println!("Hello, World!");\n}',
  },
  php: {
    label: "PHP",
    monacoLanguage: "php",
    defaultExtension: ".php",
    supportsExecution: true,
    defaultTemplate: '<?php\necho "Hello, World!";\n?>',
  },
  ruby: {
    label: "Ruby",
    monacoLanguage: "ruby",
    defaultExtension: ".rb",
    supportsExecution: true,
    defaultTemplate: '# Write your Ruby code here\nputs "Hello, World!"',
  },
  swift: {
    label: "Swift",
    monacoLanguage: "swift",
    defaultExtension: ".swift",
    supportsExecution: false,
    defaultTemplate: 'import Foundation\n\nprint("Hello, World!")',
  },
  kotlin: {
    label: "Kotlin",
    monacoLanguage: "kotlin",
    defaultExtension: ".kt",
    supportsExecution: false,
    defaultTemplate: 'fun main() {\n    println("Hello, World!")\n}',
  },
  csharp: {
    label: "C#",
    monacoLanguage: "csharp",
    defaultExtension: ".cs",
    supportsExecution: true,
    defaultTemplate: 'using System;\n\nclass Program {\n    static void Main() {\n        Console.WriteLine("Hello, World!");\n    }\n}',
  },
  sql: {
    label: "SQL",
    monacoLanguage: "sql",
    defaultExtension: ".sql",
    supportsExecution: true,
    defaultTemplate: '-- Write your SQL code here\nSELECT \'Hello, World!\' AS greeting;',
  },
  bash: {
    label: "Bash",
    monacoLanguage: "shell",
    defaultExtension: ".sh",
    supportsExecution: false,
    defaultTemplate: '#!/bin/bash\necho "Hello, World!"',
  },
  powershell: {
    label: "PowerShell",
    monacoLanguage: "powershell",
    defaultExtension: ".ps1",
    supportsExecution: false,
    defaultTemplate: '# Write your PowerShell code here\nWrite-Host "Hello, World!"',
  },
  r: {
    label: "R",
    monacoLanguage: "r",
    defaultExtension: ".r",
    supportsExecution: false,
    defaultTemplate: '# Write your R code here\nprint("Hello, World!")',
  },
  scala: {
    label: "Scala",
    monacoLanguage: "scala",
    defaultExtension: ".scala",
    supportsExecution: false,
    defaultTemplate: 'object Main extends App {\n  println("Hello, World!")\n}',
  },
  dart: {
    label: "Dart",
    monacoLanguage: "dart",
    defaultExtension: ".dart",
    supportsExecution: false,
    defaultTemplate: 'void main() {\n  print(\'Hello, World!\');\n}',
  },
  json: {
    label: "JSON",
    monacoLanguage: "json",
    defaultExtension: ".json",
    supportsExecution: false,
    defaultTemplate: '{\n  "message": "Hello, World!",\n  "version": "1.0.0"\n}',
  },
  yaml: {
    label: "YAML",
    monacoLanguage: "yaml",
    defaultExtension: ".yaml",
    supportsExecution: false,
    defaultTemplate: 'message: Hello, World!\nversion: 1.0.0\nitems:\n  - first\n  - second\n  - third',
  },
  xml: {
    label: "XML",
    monacoLanguage: "xml",
    defaultExtension: ".xml",
    supportsExecution: false,
    defaultTemplate: '<?xml version="1.0" encoding="UTF-8"?>\n<root>\n  <message>Hello, World!</message>\n</root>',
  },
  gdscript: {
    label: "GDScript",
    monacoLanguage: "gdscript",
    defaultExtension: ".gd",
    supportsExecution: true,
    defaultTemplate: "# Write your GDScript code here\nextends Node\n\nfunc _ready():\n    print(\"Hello, World!\")"
  },
  lexical: {
    label: "Lexical",
    monacoLanguage: "lexical",
    defaultExtension: ".lsp",
    supportsExecution: false,
    defaultTemplate: "% Write your Lexical code here\n"
  },
  prolog: {
    label: "Prolog",
    monacoLanguage: "prolog",
    defaultExtension: ".pl",
    supportsExecution: false,
    defaultTemplate: "% Write your Prolog code here\nhello :- write('Hello, World!'), nl.\n"
  },
  pascal: {
    label: "Pascal",
    monacoLanguage: "pascal",
    defaultExtension: ".pas",
    supportsExecution: false,
    defaultTemplate: "program HelloWorld;\nbegin\n  WriteLn('Hello, World!');\nend."
  },
  fsharp: {
    label: "F#",
    monacoLanguage: "fsharp",
    defaultExtension: ".fs",
    supportsExecution: false,
    defaultTemplate: "// Write your F# code here\nprintfn \"Hello, World!\""
  },
  haskell: {
    label: "Haskell",
    monacoLanguage: "haskell",
    defaultExtension: ".hs",
    supportsExecution: false,
    defaultTemplate: "-- Write your Haskell code here\nmain :: IO ()\nmain = putStrLn \"Hello, World!\""
  },
  perl: {
    label: "Perl",
    monacoLanguage: "perl",
    defaultExtension: ".pl",
    supportsExecution: false,
    defaultTemplate: "#!/usr/bin/perl\n# Write your Perl code here\nprint \"Hello, World!\\n\";"
  },
  groovy: {
    label: "Groovy",
    monacoLanguage: "groovy",
    defaultExtension: ".groovy",
    supportsExecution: false,
    defaultTemplate: "// Write your Groovy code here\nprintln 'Hello, World!'"
  },
  elixir: {
    label: "Elixir",
    monacoLanguage: "elixir",
    defaultExtension: ".ex",
    supportsExecution: false,
    defaultTemplate: "# Write your Elixir code here\nIO.puts \"Hello, World!\""
  },
  clojure: {
    label: "Clojure",
    monacoLanguage: "clojure",
    defaultExtension: ".clj",
    supportsExecution: false,
    defaultTemplate: "; Write your Clojure code here\n(println \"Hello, World!\")"
  },
  erlang: {
    label: "Erlang",
    monacoLanguage: "erlang",
    defaultExtension: ".erl",
    supportsExecution: false,
    defaultTemplate: "% Write your Erlang code here\n-module(hello).\n-export([world/0]).\n\nworld() -> io:format(\"Hello, World!~n\")."
  },
  fortran: {
    label: "Fortran",
    monacoLanguage: "fortran",
    defaultExtension: ".f90",
    supportsExecution: false,
    defaultTemplate: "! Write your Fortran code here\nprogram hello\n  print *, 'Hello, World!'\nend program hello"
  },
  ada: {
    label: "Ada",
    monacoLanguage: "ada",
    defaultExtension: ".adb",
    supportsExecution: false,
    defaultTemplate: "-- Write your Ada code here\nwith Ada.Text_IO; use Ada.Text_IO;\n\nprocedure Hello is\nbegin\n   Put_Line(\"Hello, World!\");\nend Hello;"
  },
  cobol: {
    label: "COBOL",
    monacoLanguage: "cobol",
    defaultExtension: ".cob",
    supportsExecution: false,
    defaultTemplate: "       IDENTIFICATION DIVISION.\n       PROGRAM-ID. HELLO.\n       PROCEDURE DIVISION.\n           DISPLAY 'Hello, World!'.\n           STOP RUN."
  },
  assembly_x86: {
    label: "Assembly (x86)",
    monacoLanguage: "asm",
    defaultExtension: ".asm",
    supportsExecution: false,
    defaultTemplate: "; Write your x86 Assembly code here\nsection .data\n    msg db 'Hello, World!', 0xa\n    len equ $ - msg\n\nsection .text\n    global _start\n\n_start:\n    mov eax, 4\n    mov ebx, 1\n    mov ecx, msg\n    mov edx, len\n    int 0x80\n\n    mov eax, 1\n    xor ebx, ebx\n    int 0x80"
  },
  assembly_arm: {
    label: "Assembly (ARM)",
    monacoLanguage: "asm",
    defaultExtension: ".arm",
    supportsExecution: false,
    defaultTemplate: "; Write your ARM Assembly code here\n.global _start\n\n.section .data\nmsg:\n    .ascii \"Hello, World!\\n\"\n    len = . - msg\n\n.section .text\n_start:\n    mov r0, #1\n    ldr r1, =msg\n    ldr r2, =len\n    mov r7, #4\n    svc #0\n\n    mov r0, #0\n    mov r7, #1\n    svc #0"
  },
  assembly_mips: {
    label: "Assembly (MIPS)",
    monacoLanguage: "mips",
    defaultExtension: ".mips",
    supportsExecution: false,
    defaultTemplate: "# Write your MIPS Assembly code here\n.data\n    msg: .asciiz \"Hello, World!\\n\"\n\n.text\n.globl main\n\nmain:\n    li $v0, 4\n    la $a0, msg\n    syscall\n\n    li $v0, 10\n    syscall"
  },
  assembly_riscv: {
    label: "Assembly (RISC-V)",
    monacoLanguage: "riscv",
    defaultExtension: ".riscv",
    supportsExecution: false,
    defaultTemplate: "# Write your RISC-V Assembly code here\n.section .data\nmsg:\n    .string \"Hello, World!\\n\"\n\n.section .text\n.globl _start\n\n_start:\n    li a0, 1\n    la a1, msg\n    li a2, 14\n    li a7, 64\n    ecall\n\n    li a0, 0\n    li a7, 93\n    ecall"
  },
  assembly_powerpc: {
    label: "Assembly (PowerPC)",
    monacoLanguage: "powerpc",
    defaultExtension: ".ppc",
    supportsExecution: false,
    defaultTemplate: "# Write your PowerPC Assembly code here\n.section .data\nmsg:\n    .string \"Hello, World!\\n\"\n\n.section .text\n.globl _start\n\n_start:\n    li 0, 4\n    lis 3, msg@ha\n    addi 3, 3, msg@l\n    li 4, 14\n    sc\n\n    li 0, 1\n    li 3, 0\n    sc"
  },
  webassembly: {
    label: "WebAssembly",
    monacoLanguage: "wasm",
    defaultExtension: ".wat",
    supportsExecution: true,
    defaultTemplate: `;; WebAssembly Text Format (WAT) with WASI support
(module
  ;; Import WASI fd_write function for printing
  (import "wasi_snapshot_preview1" "fd_write"
    (func $fd_write (param i32 i32 i32 i32) (result i32)))
  
  ;; Memory for our data
  (memory 1)
  (export "memory" (memory 0))
  
  ;; Store "Hello, World!\\n" at memory offset 0
  (data (i32.const 0) "Hello, World!\\n")
  
  ;; WASI entry point
  (func (export "_start")
    ;; iovec structure at offset 100: [ptr, len]
    (i32.store (i32.const 100) (i32.const 0))
    (i32.store (i32.const 104) (i32.const 14))
    
    ;; Call fd_write(1, 100, 1, 108)
    ;; fd=1 (stdout), iov=100, iovcnt=1, nwritten=108
    (call $fd_write
      (i32.const 1)
      (i32.const 100)
      (i32.const 1)
      (i32.const 108))
    drop))`,
  },
  forth: {
    label: "Forth",
    monacoLanguage: "forth",
    defaultExtension: ".fth",
    supportsExecution: false,
    defaultTemplate: "\\ Write your Forth code here\n: HELLO  .\" Hello, World!\" CR ;\nHELLO"
  },
  dlang: {
    label: "D",
    monacoLanguage: "d",
    defaultExtension: ".d",
    supportsExecution: false,
    defaultTemplate: "// Write your D code here\nimport std.stdio;\n\nvoid main()\n{\n    writeln(\"Hello, World!\");\n}"
  },
  nim: {
    label: "Nim",
    monacoLanguage: "nim",
    defaultExtension: ".nim",
    supportsExecution: false,
    defaultTemplate: "# Write your Nim code here\necho \"Hello, World!\""
  },
  zig: {
    label: "Zig",
    monacoLanguage: "zig",
    defaultExtension: ".zig",
    supportsExecution: false,
    defaultTemplate: "// Write your Zig code here\nconst std = @import(\"std\");\n\npub fn main() !void {\n    const stdout = std.io.getStdOut().writer();\n    try stdout.print(\"Hello, World!\\n\", .{});\n}"
  },
  v: {
    label: "V",
    monacoLanguage: "v",
    defaultExtension: ".v",
    supportsExecution: false,
    defaultTemplate: "// Write your V code here\nfn main() {\n\tprintln('Hello, World!')\n}"
  },
  crystal: {
    label: "Crystal",
    monacoLanguage: "crystal",
    defaultExtension: ".cr",
    supportsExecution: false,
    defaultTemplate: "# Write your Crystal code here\nputs \"Hello, World!\""
  },
  ocaml: {
    label: "OCaml",
    monacoLanguage: "ocaml",
    defaultExtension: ".ml",
    supportsExecution: false,
    defaultTemplate: "(* Write your OCaml code here *)\nlet () = print_endline \"Hello, World!\""
  },
  scheme: {
    label: "Scheme",
    monacoLanguage: "scheme",
    defaultExtension: ".scm",
    supportsExecution: false,
    defaultTemplate: "; Write your Scheme code here\n(display \"Hello, World!\")\n(newline)"
  },
  smalltalk: {
    label: "Smalltalk",
    monacoLanguage: "smalltalk",
    defaultExtension: ".st",
    supportsExecution: false,
    defaultTemplate: "\"Write your Smalltalk code here\"\nTranscript show: 'Hello, World!'.\nTranscript cr."
  },
  julia: {
    label: "Julia",
    monacoLanguage: "julia",
    defaultExtension: ".jl",
    supportsExecution: false,
    defaultTemplate: "# Write your Julia code here\nprintln(\"Hello, World!\")"
  },
  vb: {
    label: "Visual Basic",
    monacoLanguage: "vb",
    defaultExtension: ".vb",
    supportsExecution: false,
    defaultTemplate: "' Write your Visual Basic code here\nModule Program\n    Sub Main()\n        Console.WriteLine(\"Hello, World!\")\n    End Sub\nEnd Module"
  },
  objectpascal: {
    label: "Object Pascal",
    monacoLanguage: "pascal",
    defaultExtension: ".pas",
    supportsExecution: false,
    defaultTemplate: "// Write your Object Pascal code here\nprogram HelloWorld;\n\n{$mode objfpc}{$H+}\n\nuses\n  Classes, SysUtils;\n\nbegin\n  WriteLn('Hello, World!');\nend."
  },
  hope: {
    label: "Hope",
    monacoLanguage: "hope",
    defaultExtension: ".hop",
    supportsExecution: false,
    defaultTemplate: "! Write your Hope code here\ndec hello : () -> list(char);\n--- hello() <= \"Hello, World!\";"
  },
  b: {
    label: "B",
    monacoLanguage: "b",
    defaultExtension: ".b",
    supportsExecution: false,
    defaultTemplate: "/* Write your B code here */\nmain() {\n    extrn putstr;\n    putstr(\"Hello, World!*n\");\n}"
  },
  bcpl: {
    label: "BCPL",
    monacoLanguage: "bcpl",
    defaultExtension: ".bcpl",
    supportsExecution: false,
    defaultTemplate: "// Write your BCPL code here\nGET \"libhdr\"\n\nLET start() = VALOF\n{ writes(\"Hello, World!*n\")\n  RESULTIS 0\n}"
  },
  fantom: {
    label: "Fantom",
    monacoLanguage: "fan",
    defaultExtension: ".fan",
    supportsExecution: false,
    defaultTemplate: "// Write your Fantom code here\nclass Hello\n{\n  static Void main()\n  {\n    echo(\"Hello, World!\")\n  }\n}"
  },
  modula3: {
    label: "Modula-3",
    monacoLanguage: "modula3",
    defaultExtension: ".m3",
    supportsExecution: false,
    defaultTemplate: "(* Write your Modula-3 code here *)\nMODULE Hello EXPORTS Main;\n\nIMPORT IO;\n\nBEGIN\n  IO.Put(\"Hello, World!\\n\");\nEND Hello."
  },
  fstar: {
    label: "F*",
    monacoLanguage: "fstar",
    defaultExtension: ".fst",
    supportsExecution: false,
    defaultTemplate: "(* Write your F* code here *)\nmodule Hello\n\nlet main () : ML unit =\n  FStar.IO.print_string \"Hello, World!\\n\""
  },
  elm: {
    label: "Elm",
    monacoLanguage: "elm",
    defaultExtension: ".elm",
    supportsExecution: false,
    defaultTemplate: "-- Write your Elm code here\nmodule Main exposing (main)\n\nimport Html exposing (text)\n\nmain =\n    text \"Hello, World!\""
  },
  haxe: {
    label: "Haxe",
    monacoLanguage: "haxe",
    defaultExtension: ".hx",
    supportsExecution: false,
    defaultTemplate: "// Write your Haxe code here\nclass Main {\n    static public function main() {\n        trace(\"Hello, World!\");\n    }\n}"
  },
  gleam: {
    label: "Gleam",
    monacoLanguage: "gleam",
    defaultExtension: ".gleam",
    supportsExecution: false,
    defaultTemplate: "// Write your Gleam code here\nimport gleam/io\n\npub fn main() {\n  io.println(\"Hello, World!\")\n}"
  },
  rescript: {
    label: "ReScript",
    monacoLanguage: "rescript",
    defaultExtension: ".res",
    supportsExecution: false,
    defaultTemplate: "// Write your ReScript code here\nJs.log(\"Hello, World!\")"
  },
  csharp_script: {
    label: "C# Script",
    monacoLanguage: "csharp_script",
    defaultExtension: ".csx",
    supportsExecution: false,
    defaultTemplate: "// Write your C# Script code here\nusing System;\n\nConsole.WriteLine(\"Hello, World!\");"
  },
  fsharp_script: {
    label: "F# Script",
    monacoLanguage: "fsharp_script",
    defaultExtension: ".fsx",
    supportsExecution: false,
    defaultTemplate: "// Write your F# Script code here\nprintfn \"Hello, World!\""
  },
  assemblyscript: {
    label: "AssemblyScript",
    monacoLanguage: "typescript",
    defaultExtension: ".as.ts",
    supportsExecution: false,
    defaultTemplate: "// Write your AssemblyScript code here\nexport function sayHello(): void {\n  trace(\"Hello, World!\");\n}\n\nsayHello();"
  },
  factor: {
    label: "Factor",
    monacoLanguage: "factor",
    defaultExtension: ".factor",
    supportsExecution: false,
    defaultTemplate: "! Write your Factor code here\nUSING: io ;\nIN: hello\n\n: hello ( -- ) \"Hello, World!\" print ;\n\nMAIN: hello"
  },
  purescript: {
    label: "PureScript",
    monacoLanguage: "purescript",
    defaultExtension: ".purs",
    supportsExecution: false,
    defaultTemplate: "-- Write your PureScript code here\nmodule Main where\n\nimport Prelude\nimport Effect (Effect)\nimport Effect.Console (log)\n\nmain :: Effect Unit\nmain = log \"Hello, World!\""
  }
}

// Helper function to detect language from file extension
export function getLanguageFromExtension(filename: string): SupportedLanguage {
  const ext = filename.split('.').pop()?.toLowerCase()
  
  // Casos especiais com múltiplas extensões
  switch (ext) {
    case 'js': return 'javascript'
    case 'ts':
    case 'tsx': return 'typescript'
    case 'py': return 'python'
    case 'lua': return 'lua'
    case 'c':
    case 'h': return 'c'
    case 'cpp':
    case 'cc':
    case 'cxx':
    case 'hpp': return 'cpp'
    case 'html':
    case 'htm': return 'html'
    case 'css': return 'css'
    case 'md':
    case 'markdown': return 'markdown'
    case 'java': return 'java'
    case 'go': return 'go'
    case 'rs': return 'rust'
    case 'php': return 'php'
    case 'rb': return 'ruby'
    case 'swift': return 'swift'
    case 'kt':
    case 'kts': return 'kotlin'
    case 'cs': return 'csharp'
    case 'csx': return 'csharp_script'
    case 'sql': return 'sql'
    case 'sh':
    case 'bash': return 'bash'
    case 'ps1': return 'powershell'
    case 'r': return 'r'
    case 'scala':
    case 'sc': return 'scala'
    case 'dart': return 'dart'
    case 'json': return 'json'
    case 'yaml':
    case 'yml': return 'yaml'
    case 'xml': return 'xml'
    case 'gd': return 'gdscript'
    case 'lsp':
    case 'lexical': return 'lexical'
    case 'pro': return 'prolog'
    case 'pas':
    case 'pp': return 'pascal'
    case 'fs': return 'fsharp'
    case 'fsx': return 'fsharp_script'
    case 'hs':
    case 'lhs': return 'haskell'
    case 'pl':
    case 'pm': return 'perl'
    case 'groovy':
    case 'gvy': return 'groovy'
    case 'ex':
    case 'exs': return 'elixir'
    case 'clj':
    case 'cljs':
    case 'cljc': return 'clojure'
    case 'erl':
    case 'hrl': return 'erlang'
    case 'f':
    case 'for':
    case 'f90':
    case 'f95': return 'fortran'
    case 'ada':
    case 'adb':
    case 'ads': return 'ada'
    case 'cob':
    case 'cbl': return 'cobol'
    case 'asm':
    case 's': return 'assembly_x86'
    case 'arm': return 'assembly_arm'
    case 'mips': return 'assembly_mips'
    case 'riscv': return 'assembly_riscv'
    case 'ppc': return 'assembly_powerpc'
    case 'wat': return 'webassembly'
    case 'fth':
    case 'forth': return 'forth'
    case 'd':
    case 'di': return 'dlang'
    case 'nim':
    case 'nims':
    case 'nimble': return 'nim'
    case 'zig': return 'zig'
    case 'v':
    case 'vv': return 'v'
    case 'cr': return 'crystal'
    case 'ml':
    case 'mli': return 'ocaml'
    case 'scm':
    case 'ss':
    case 'rkt': return 'scheme'
    case 'st': return 'smalltalk'
    case 'jl': return 'julia'
    case 'vb':
    case 'vbs': return 'vb'
    case 'hop': return 'hope'
    case 'b': return 'b'
    case 'bcpl': return 'bcpl'
    case 'fan': return 'fantom'
    case 'm3':
    case 'i3': return 'modula3'
    case 'fst':
    case 'fsti': return 'fstar'
    case 'elm': return 'elm'
    case 'hx': return 'haxe'
    case 'gleam': return 'gleam'
    case 'res':
    case 'resi': return 'rescript'
    case 'factor': return 'factor'
    case 'purs': return 'purescript'
    default: return 'javascript'
  }
}

// Helper function to get Monaco language from our language type
export function getMonacoLanguage(language: SupportedLanguage): string {
  return LANGUAGE_CONFIGS[language].monacoLanguage
}

// Helper function to get valid file extensions for a language
export function getValidExtensions(language: SupportedLanguage): string[] {
  switch (language) {
    case 'javascript': return ['.js']
    case 'typescript': return ['.ts', '.tsx']
    case 'python': return ['.py']
    case 'lua': return ['.lua']
    case 'c': return ['.c', '.h']
    case 'cpp': return ['.cpp', '.cc', '.cxx', '.hpp', '.h']
    case 'html': return ['.html', '.htm']
    case 'css': return ['.css']
    case 'markdown': return ['.md', '.markdown']
    case 'java': return ['.java']
    case 'go': return ['.go']
    case 'rust': return ['.rs']
    case 'php': return ['.php']
    case 'ruby': return ['.rb']
    case 'swift': return ['.swift']
    case 'kotlin': return ['.kt', '.kts']
    case 'csharp': return ['.cs']
    case 'csharp_script': return ['.csx']
    case 'sql': return ['.sql']
    case 'bash': return ['.sh', '.bash']
    case 'powershell': return ['.ps1']
    case 'r': return ['.r']
    case 'scala': return ['.scala', '.sc']
    case 'dart': return ['.dart']
    case 'json': return ['.json']
    case 'yaml': return ['.yaml', '.yml']
    case 'xml': return ['.xml']
    case 'gdscript': return ['.gd']
    case 'lexical': return ['.lsp', '.lexical']
    case 'prolog': return ['.pl', '.pro']
    case 'pascal': return ['.pas', '.pp']
    case 'objectpascal': return ['.pas']
    case 'fsharp': return ['.fs']
    case 'fsharp_script': return ['.fsx']
    case 'haskell': return ['.hs', '.lhs']
    case 'perl': return ['.pl', '.pm']
    case 'groovy': return ['.groovy', '.gvy']
    case 'elixir': return ['.ex', '.exs']
    case 'clojure': return ['.clj', '.cljs', '.cljc']
    case 'erlang': return ['.erl', '.hrl']
    case 'fortran': return ['.f', '.for', '.f90', '.f95']
    case 'ada': return ['.ada', '.adb', '.ads']
    case 'cobol': return ['.cob', '.cbl']
    case 'assembly_x86': return ['.asm', '.s']
    case 'assembly_arm': return ['.arm']
    case 'assembly_mips': return ['.mips']
    case 'assembly_riscv': return ['.riscv']
    case 'assembly_powerpc': return ['.ppc']
    case 'webassembly': return ['.wat']
    case 'forth': return ['.fth', '.forth']
    case 'dlang': return ['.d', '.di']
    case 'nim': return ['.nim', '.nims', '.nimble']
    case 'zig': return ['.zig']
    case 'v': return ['.v', '.vv']
    case 'crystal': return ['.cr']
    case 'ocaml': return ['.ml', '.mli']
    case 'scheme': return ['.scm', '.ss', '.rkt']
    case 'smalltalk': return ['.st']
    case 'julia': return ['.jl']
    case 'vb': return ['.vb', '.vbs']
    case 'hope': return ['.hop']
    case 'b': return ['.b']
    case 'bcpl': return ['.bcpl']
    case 'fantom': return ['.fan']
    case 'modula3': return ['.m3', '.i3']
    case 'fstar': return ['.fst', '.fsti']
    case 'elm': return ['.elm']
    case 'haxe': return ['.hx']
    case 'gleam': return ['.gleam']
    case 'rescript': return ['.res', '.resi']
    case 'assemblyscript': return ['.as.ts']
    case 'factor': return ['.factor']
    case 'purescript': return ['.purs']
    default: return ['.txt']
  }
}

// Helper function to check if a file has a valid extension for a language
export function hasValidExtension(filePath: string, language: SupportedLanguage): boolean {
  const validExtensions = getValidExtensions(language)
  const lowerPath = filePath.toLowerCase()
  return validExtensions.some(ext => lowerPath.endsWith(ext))
}

// Shiki supported languages for syntax highlighting
export const SHIKI_LANGS = [
  'javascript',
  'typescript',
  'python',
  'lua',
  'c',
  'cpp',
  'html',
  'css',
  'markdown',
  'java',
  'go',
  'rust',
  'php',
  'ruby',
  'swift',
  'kotlin',
  'csharp',
  'sql',
  'bash',
  'powershell',
  'r',
  'scala',
  'dart',
  'json',
  'yaml',
  'xml',
  'prolog',
  'pascal',
  'fsharp',
  'haskell',
  'riscv',
  'wasm',
  'ocaml',
]
