import React from "react";
import type { CodeFile, LanguageType, ProgrammingLanguage } from "./types";

// Get the base name of a file (without extension)
export const getBaseName = (fileName: string): string => {
  const lastDotIndex = fileName.lastIndexOf(".")
  return lastDotIndex !== -1 ? fileName.substring(0, lastDotIndex) : fileName
}

// Get the extension of a file
export const getExtension = (fileName: string): string => {
  const lastDotIndex = fileName.lastIndexOf(".")
  return lastDotIndex !== -1 ? fileName.substring(lastDotIndex + 1) : ""
}

// Update the getExtensionForSelectedLanguage function to handle C and C++
export const getExtensionForSelectedLanguage = (selectedLanguage: ProgrammingLanguage): string => {
  switch (selectedLanguage) {
    case "javascript":
      return "js"
    case "typescript":
      return "ts"
    case "python":
      return "py"
    case "lua":
      return "lua"
    case "c":
      return "c"
    case "h":
      return "h"
    case "cpp":
      return "cpp"
    case "hpp":
      return "hpp"
    default:
      return "js"
  }
}

// Get the content for a file
export const getFileContent = (file: CodeFile, _selectedLanguage?: unknown): string => {
  return file?.content || ""
}

// Update the getExtensionForLanguage function to handle C and C++
export const getExtensionForLanguage = (lang: LanguageType): string => {
  switch (lang) {
    case "javascript":
      return "js"
    case "typescript":
      return "ts"
    case "python":
      return "py"
    case "lua":
      return "lua"
    case "c":
      return "c"
    case "cpp":
      return "cpp"
    case "cheader":
      return "h"
    case "cppheader":
      return "hpp"
    case "h":
      return "h"
    case "hpp":
      return "hpp"
    case "html":
      return "html"
    case "css":
      return "css"
    case "json":
      return "json"
    case "xml":
      return "xml"
    case "yaml":
      return "yaml"
    default:
      return "txt"
  }
}

// Update the getLanguageFromExtension function to handle C and C++
export const getLanguageFromExtension = (ext: string): LanguageType => {
  switch (ext) {
    case "js":
      return "javascript"
    case "ts":
      return "typescript"
    case "py":
      return "python"
    case "lua":
      return "lua"
    case "c":
      return "c"
    case "cpp":
      return "cpp"
    case "h":
      return "h"
    case "hpp":
      return "hpp"
    case "html":
      return "html"
    case "css":
      return "css"
    case "json":
      return "json"
    case "xml":
      return "xml"
    case "yaml":
      return "yaml"
    default:
      return "text"
  }
}

// Update the getLanguageLabel function to handle C and C++
export const getLanguageLabel = (lang: LanguageType | string): string => {
  switch (lang) {
    case "javascript":
      return ".js (JavaScript)"
    case "typescript":
      return ".ts (TypeScript)"
    case "python":
      return ".py (Python)"
    case "lua":
      return ".lua (Lua)"
    case "c":
      return ".c (C)"
    case "cpp":
      return ".cpp (C++)"
    case "h":
      return ".h (C Header)"
    case "hpp":
      return ".hpp (C++ Header)"
    case "cheader":
      return ".h (C Header)"
    case "cppheader":
      return ".hpp (C++ Header)"
    case "html":
      return ".html (HTML)"
    case "css":
      return ".css (CSS)"
    case "json":
      return ".json (JSON)"
    case "text":
      return ".txt (Plain Text)"
    case "xml":
      return ".xml (XML)"
    case "yaml":
      return ".yaml (YAML)"
    default:
      return ".txt (TXT)"
  }
}

// Update the getFileIcon function to handle C and C++
export const getFileIcon = (file: CodeFile, selectedLanguage?: ProgrammingLanguage) => {
  // Use the file's own language for the icon
  const extension = getExtension(file.name)

  switch (extension) {
    case "js":
      return React.createElement("span", { className: "text-yellow-500" }, "JS")
    case "ts":
      return React.createElement("span", { className: "text-blue-500" }, "TS")
    case "py":
      return React.createElement("span", { className: "text-green-500" }, "PY")
    case "lua":
      return React.createElement("span", { className: "text-indigo-500" }, "LUA")
    case "c":
      return React.createElement("span", { className: "text-blue-700" }, "C")
    case "h":
      return React.createElement("span", { className: "text-blue-700" }, "H")
    case "cpp":
    case "cc":
    case "cxx":
      return React.createElement("span", { className: "text-blue-600" }, "C++")
    case "hpp":
      return React.createElement("span", { className: "text-blue-600" }, "HPP")
    case "html":
      return React.createElement("span", { className: "text-orange-500" }, "HTML")
    case "css":
      return React.createElement("span", { className: "text-purple-500" }, "CSS")
    case "json":
      return React.createElement("span", { className: "text-gray-500" }, "JSON")
    case "sh":
      return React.createElement("span", { className: "text-gray-500" }, "SH")
    case "xml":
      return React.createElement("span", { className: "text-red-500" }, "XML")
    case "yaml":
      return React.createElement("span", { className: "text-amber-500" }, "YAML")
    default:
      return React.createElement("span", { className: "text-gray-500" }, "TXT")
  }
}

export const getStateIcon = (file: CodeFile) => {
  // Since we removed hasStates, we can return a simple icon or nothing
  return null
}
