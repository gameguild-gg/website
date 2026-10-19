export class CppCompletionProvider {
  private monaco: typeof import("monaco-editor")

  constructor(monaco: typeof import("monaco-editor")) {
    this.monaco = monaco
  }

  register() {
    // Register a completion item provider for C++
    return this.monaco.languages.registerCompletionItemProvider("cpp", {
      provideCompletionItems: (model, position) => {
        const word = model.getWordUntilPosition(position)
        const range = {
          startLineNumber: position.lineNumber,
          endLineNumber: position.lineNumber,
          startColumn: word.startColumn,
          endColumn: word.endColumn,
        }

        // Standard C++ keywords
        const suggestions = [
          // C++ keywords
          { label: "class", kind: this.monaco.languages.CompletionItemKind.Keyword, insertText: "class", range },
          { label: "struct", kind: this.monaco.languages.CompletionItemKind.Keyword, insertText: "struct", range },
          {
            label: "namespace",
            kind: this.monaco.languages.CompletionItemKind.Keyword,
            insertText: "namespace",
            range,
          },
          { label: "template", kind: this.monaco.languages.CompletionItemKind.Keyword, insertText: "template", range },
          { label: "typename", kind: this.monaco.languages.CompletionItemKind.Keyword, insertText: "typename", range },
          { label: "const", kind: this.monaco.languages.CompletionItemKind.Keyword, insertText: "const", range },
          { label: "static", kind: this.monaco.languages.CompletionItemKind.Keyword, insertText: "static", range },
          { label: "virtual", kind: this.monaco.languages.CompletionItemKind.Keyword, insertText: "virtual", range },
          { label: "override", kind: this.monaco.languages.CompletionItemKind.Keyword, insertText: "override", range },
          { label: "final", kind: this.monaco.languages.CompletionItemKind.Keyword, insertText: "final", range },
          { label: "public", kind: this.monaco.languages.CompletionItemKind.Keyword, insertText: "public", range },
          { label: "private", kind: this.monaco.languages.CompletionItemKind.Keyword, insertText: "private", range },
          {
            label: "protected",
            kind: this.monaco.languages.CompletionItemKind.Keyword,
            insertText: "protected",
            range,
          },
          { label: "friend", kind: this.monaco.languages.CompletionItemKind.Keyword, insertText: "friend", range },
          { label: "using", kind: this.monaco.languages.CompletionItemKind.Keyword, insertText: "using", range },
          { label: "typedef", kind: this.monaco.languages.CompletionItemKind.Keyword, insertText: "typedef", range },

          // Control flow
          {
            label: "if",
            kind: this.monaco.languages.CompletionItemKind.Keyword,
            insertText: "if (${1:condition}) {\n\t${2}\n}",
            insertTextRules: this.monaco.languages.CompletionItemInsertTextRule.InsertAsSnippet,
            range,
          },
          {
            label: "else",
            kind: this.monaco.languages.CompletionItemKind.Keyword,
            insertText: "else {\n\t${1}\n}",
            insertTextRules: this.monaco.languages.CompletionItemInsertTextRule.InsertAsSnippet,
            range,
          },
          {
            label: "for",
            kind: this.monaco.languages.CompletionItemKind.Keyword,
            insertText: "for (${1:int i = 0}; ${2:i < n}; ${3:i++}) {\n\t${4}\n}",
            insertTextRules: this.monaco.languages.CompletionItemInsertTextRule.InsertAsSnippet,
            range,
          },
          {
            label: "while",
            kind: this.monaco.languages.CompletionItemKind.Keyword,
            insertText: "while (${1:condition}) {\n\t${2}\n}",
            insertTextRules: this.monaco.languages.CompletionItemInsertTextRule.InsertAsSnippet,
            range,
          },
          {
            label: "do",
            kind: this.monaco.languages.CompletionItemKind.Keyword,
            insertText: "do {\n\t${1}\n} while (${2:condition});",
            insertTextRules: this.monaco.languages.CompletionItemInsertTextRule.InsertAsSnippet,
            range,
          },
          {
            label: "switch",
            kind: this.monaco.languages.CompletionItemKind.Keyword,
            insertText:
              "switch (${1:expression}) {\n\tcase ${2:value}:\n\t\t${3}\n\t\tbreak;\n\tdefault:\n\t\t${4}\n\t\tbreak;\n}",
            insertTextRules: this.monaco.languages.CompletionItemInsertTextRule.InsertAsSnippet,
            range,
          },

          // Types
          { label: "int", kind: this.monaco.languages.CompletionItemKind.Keyword, insertText: "int", range },
          { label: "char", kind: this.monaco.languages.CompletionItemKind.Keyword, insertText: "char", range },
          { label: "bool", kind: this.monaco.languages.CompletionItemKind.Keyword, insertText: "bool", range },
          { label: "float", kind: this.monaco.languages.CompletionItemKind.Keyword, insertText: "float", range },
          { label: "double", kind: this.monaco.languages.CompletionItemKind.Keyword, insertText: "double", range },
          { label: "void", kind: this.monaco.languages.CompletionItemKind.Keyword, insertText: "void", range },
          { label: "unsigned", kind: this.monaco.languages.CompletionItemKind.Keyword, insertText: "unsigned", range },
          { label: "long", kind: this.monaco.languages.CompletionItemKind.Keyword, insertText: "long", range },
          { label: "short", kind: this.monaco.languages.CompletionItemKind.Keyword, insertText: "short", range },

          // Standard library
          {
            label: "std::string",
            kind: this.monaco.languages.CompletionItemKind.Class,
            insertText: "std::string",
            range,
          },
          {
            label: "std::vector",
            kind: this.monaco.languages.CompletionItemKind.Class,
            insertText: "std::vector<${1:T}>",
            insertTextRules: this.monaco.languages.CompletionItemInsertTextRule.InsertAsSnippet,
            range,
          },
          {
            label: "std::map",
            kind: this.monaco.languages.CompletionItemKind.Class,
            insertText: "std::map<${1:Key}, ${2:Value}>",
            insertTextRules: this.monaco.languages.CompletionItemInsertTextRule.InsertAsSnippet,
            range,
          },
          {
            label: "std::set",
            kind: this.monaco.languages.CompletionItemKind.Class,
            insertText: "std::set<${1:T}>",
            insertTextRules: this.monaco.languages.CompletionItemInsertTextRule.InsertAsSnippet,
            range,
          },
          {
            label: "std::unordered_map",
            kind: this.monaco.languages.CompletionItemKind.Class,
            insertText: "std::unordered_map<${1:Key}, ${2:Value}>",
            insertTextRules: this.monaco.languages.CompletionItemInsertTextRule.InsertAsSnippet,
            range,
          },
          {
            label: "std::unordered_set",
            kind: this.monaco.languages.CompletionItemKind.Class,
            insertText: "std::unordered_set<${1:T}>",
            insertTextRules: this.monaco.languages.CompletionItemInsertTextRule.InsertAsSnippet,
            range,
          },
          {
            label: "std::pair",
            kind: this.monaco.languages.CompletionItemKind.Class,
            insertText: "std::pair<${1:T1}, ${2:T2}>",
            insertTextRules: this.monaco.languages.CompletionItemInsertTextRule.InsertAsSnippet,
            range,
          },
          {
            label: "std::make_pair",
            kind: this.monaco.languages.CompletionItemKind.Function,
            insertText: "std::make_pair(${1:first}, ${2:second})",
            insertTextRules: this.monaco.languages.CompletionItemInsertTextRule.InsertAsSnippet,
            range,
          },

          // Common includes
          {
            label: "#include <iostream>",
            kind: this.monaco.languages.CompletionItemKind.Snippet,
            insertText: "#include <iostream>",
            range,
          },
          {
            label: "#include <vector>",
            kind: this.monaco.languages.CompletionItemKind.Snippet,
            insertText: "#include <vector>",
            range,
          },
          {
            label: "#include <string>",
            kind: this.monaco.languages.CompletionItemKind.Snippet,
            insertText: "#include <string>",
            range,
          },
          {
            label: "#include <map>",
            kind: this.monaco.languages.CompletionItemKind.Snippet,
            insertText: "#include <map>",
            range,
          },
          {
            label: "#include <set>",
            kind: this.monaco.languages.CompletionItemKind.Snippet,
            insertText: "#include <set>",
            range,
          },
          {
            label: "#include <algorithm>",
            kind: this.monaco.languages.CompletionItemKind.Snippet,
            insertText: "#include <algorithm>",
            range,
          },
          {
            label: "#include <memory>",
            kind: this.monaco.languages.CompletionItemKind.Snippet,
            insertText: "#include <memory>",
            range,
          },

          // Common snippets
          {
            label: "cout",
            kind: this.monaco.languages.CompletionItemKind.Snippet,
            insertText: "std::cout << ${1} << std::endl;",
            insertTextRules: this.monaco.languages.CompletionItemInsertTextRule.InsertAsSnippet,
            range,
          },
          {
            label: "cin",
            kind: this.monaco.languages.CompletionItemKind.Snippet,
            insertText: "std::cin >> ${1};",
            insertTextRules: this.monaco.languages.CompletionItemInsertTextRule.InsertAsSnippet,
            range,
          },
          {
            label: "main",
            kind: this.monaco.languages.CompletionItemKind.Snippet,
            insertText: "int main(int argc, char* argv[]) {\n\t${1}\n\treturn 0;\n}",
            insertTextRules: this.monaco.languages.CompletionItemInsertTextRule.InsertAsSnippet,
            range,
          },
          {
            label: "class",
            kind: this.monaco.languages.CompletionItemKind.Snippet,
            insertText:
              "class ${1:ClassName} {\npublic:\n\t${1:ClassName}() {}\n\t~${1:ClassName}() {}\n\nprivate:\n\t${2}\n};",
            insertTextRules: this.monaco.languages.CompletionItemInsertTextRule.InsertAsSnippet,
            range,
          },
        ]

        return { suggestions }
      },
    })
  }
}
