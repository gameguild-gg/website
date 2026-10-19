"use client"

import type React from "react"

import { useRef, useEffect, useState } from "react"
import Editor, { useMonaco } from "@monaco-editor/react"
import { GripVertical } from "lucide-react"
import { cn } from "@/lib/utils"
import { PythonLanguageService } from "./python/python-language-service"
import { LuaLanguageService } from "./lua/lua-language-service"
import { CLanguageService } from "./c/c-language-service"
import { CppLanguageService } from "./cpp/cpp-language-service"
import { XMLLanguageService } from "./xml/xml-language-service"
import { YAMLLanguageService } from "./yaml/yaml-language-service"
import { editor } from "monaco-editor"

export interface CodeEditorProps {
  codeEditorHeight: number
  activeFileLanguage: string
  activeFileContent: string
  isDarkTheme: boolean
  readonly: boolean
  isEditing: boolean
  updateActiveFileContent: (content: string) => void
  handleCodeEditorResize: (e: React.MouseEvent, startY: number) => void
  onEditorMount?: (editor: editor.IStandaloneCodeEditor, monaco: typeof import("monaco-editor")) => void
  isAutocompleteEnabled?: boolean
}

export function CodeEditor({
  codeEditorHeight,
  activeFileLanguage,
  activeFileContent,
  isDarkTheme,
  readonly,
  isEditing,
  updateActiveFileContent,
  handleCodeEditorResize,
  onEditorMount,
  isAutocompleteEnabled,
}: CodeEditorProps) {
  const editorRef = useRef<editor.IStandaloneCodeEditor | null>(null)
  const monaco = useMonaco()
  const [isPythonEnabled, setIsPythonEnabled] = useState(false)
  const [isLuaEnabled, setIsLuaEnabled] = useState(false)
  const [isCEnabled, setIsCEnabled] = useState(false)
  const [isCppEnabled, setIsCppEnabled] = useState(false)
  const [isXMLEnabled, setIsXMLEnabled] = useState(false)
  const [isYAMLEnabled, setIsYAMLEnabled] = useState(false)
  const [isCHeaderEnabled, setIsCHeaderEnabled] = useState(false)
  const [isCppHeaderEnabled, setIsCppHeaderEnabled] = useState(false)

  useEffect(() => {
    if (editorRef.current) {
      editorRef.current.layout()
    }
  }, [codeEditorHeight])

  useEffect(() => {
    if (editorRef.current) {
      editorRef.current.updateOptions({ readOnly: readonly })
    }
  }, [readonly])

  useEffect(() => {
    if (editorRef.current) {
      editorRef.current.updateOptions({ theme: isDarkTheme ? "vs-dark" : "vs-light" })
    }
  }, [isDarkTheme])

  useEffect(() => {
    if (editorRef.current && monaco) {
      const model = editorRef.current.getModel()
      if (model) {
        // For language-specific files, use the selected language
        // For regular files, use the file's language property
        const editorLanguage =
          activeFileLanguage === "javascript" ||
          activeFileLanguage === "typescript" ||
          activeFileLanguage === "python" ||
          activeFileLanguage === "lua" ||
          activeFileLanguage === "c" ||
          activeFileLanguage === "cpp" ||
          activeFileLanguage === "cheader" ||
          activeFileLanguage === "cppheader" ||
          activeFileLanguage === "xml" ||
          activeFileLanguage === "yaml"
            ? activeFileLanguage
            : "plaintext"

        console.log(`Setting Monaco editor language to: ${editorLanguage}`)
        monaco.editor.setModelLanguage(model, editorLanguage)

        // Enable language services if needed
        setIsPythonEnabled(editorLanguage === "python")
        setIsLuaEnabled(editorLanguage === "lua")
        setIsCEnabled(editorLanguage === "c")
        setIsCppEnabled(editorLanguage === "cpp")
        setIsXMLEnabled(editorLanguage === "xml")
        setIsYAMLEnabled(editorLanguage === "yaml")
        setIsCHeaderEnabled(editorLanguage === "cheader")
        setIsCppHeaderEnabled(editorLanguage === "cppheader")
      }
    }
  }, [activeFileLanguage, monaco])

  const handleEditorWillMount = (monaco: typeof import("monaco-editor")) => {
    // here is the place to define custom languages and editor themes
  }

  const handleEditorDidMount = (editor: editor.IStandaloneCodeEditor, monaco: typeof import("monaco-editor")) => {
    editorRef.current = editor

    // Set the language explicitly when the editor mounts
    const editorLanguage =
      activeFileLanguage === "javascript" ||
      activeFileLanguage === "typescript" ||
      activeFileLanguage === "python" ||
      activeFileLanguage === "lua" ||
      activeFileLanguage === "c" ||
      activeFileLanguage === "cpp" ||
      activeFileLanguage === "cheader" ||
      activeFileLanguage === "cppheader" ||
      activeFileLanguage === "xml" ||
      activeFileLanguage === "yaml"
        ? activeFileLanguage
        : "plaintext"

    console.log(`Initial Monaco editor language set to: ${editorLanguage}`)
    monaco.editor.setModelLanguage(editor.getModel()!, editorLanguage)

    // Apply autocomplete settings
    editor.updateOptions({
      quickSuggestions: isAutocompleteEnabled !== false,
      suggestOnTriggerCharacters: isAutocompleteEnabled !== false,
      acceptSuggestionOnEnter: isAutocompleteEnabled !== false ? "on" : "off",
      tabCompletion: isAutocompleteEnabled !== false ? "on" : "off",
      snippetSuggestions: isAutocompleteEnabled !== false ? "inline" : "none",
    })

    // Enable language services if needed
    setIsPythonEnabled(editorLanguage === "python")
    setIsLuaEnabled(editorLanguage === "lua")
    setIsCEnabled(editorLanguage === "c")
    setIsCppEnabled(editorLanguage === "cpp")
    setIsXMLEnabled(editorLanguage === "xml")
    setIsYAMLEnabled(editorLanguage === "yaml")
    setIsCHeaderEnabled(editorLanguage === "cheader")
    setIsCppHeaderEnabled(editorLanguage === "cppheader")

    // Add a listener for language changes
    editor.onDidChangeModelLanguage((e) => {
      console.log(`Language changed to: ${e.newLanguage}`)
      setIsPythonEnabled(e.newLanguage === "python")
      setIsLuaEnabled(e.newLanguage === "lua")
      setIsCEnabled(e.newLanguage === "c")
      setIsCppEnabled(e.newLanguage === "cpp")
      setIsXMLEnabled(e.newLanguage === "xml")
      setIsYAMLEnabled(e.newLanguage === "yaml")
      setIsCHeaderEnabled(e.newLanguage === "cheader")
      setIsCppHeaderEnabled(e.newLanguage === "cppheader")
    })

    onEditorMount?.(editor, monaco)
  }

  const handleChange = (value: string | undefined) => {
    if (value && !readonly) {
      updateActiveFileContent(value)
    }
  }

  useEffect(() => {
    if (editorRef.current) {
      editorRef.current.updateOptions({
        quickSuggestions: isAutocompleteEnabled !== false,
        suggestOnTriggerCharacters: isAutocompleteEnabled !== false,
        acceptSuggestionOnEnter: isAutocompleteEnabled !== false ? "on" : "off",
        tabCompletion: isAutocompleteEnabled !== false ? "on" : "off",
        snippetSuggestions: isAutocompleteEnabled !== false ? "inline" : "none",
      })
    }
  }, [isAutocompleteEnabled])

  return (
    <div className="monaco-editor-container">
      <Editor
        height={`${codeEditorHeight}px`}
        language={activeFileLanguage}
        theme={isDarkTheme ? "vs-dark" : "vs-light"}
        value={activeFileContent}
        options={{
          wordWrap: "on",
          minimap: { enabled: false },
          readOnly: readonly,
          fontSize: 14,
          scrollBeyondLastLine: false,
          automaticLayout: true,
          // Float hovers/suggest widgets above the modal chrome.
          fixedOverflowWidgets: true,
          quickSuggestions: isAutocompleteEnabled !== false,
          suggestOnTriggerCharacters: isAutocompleteEnabled !== false,
          acceptSuggestionOnEnter: isAutocompleteEnabled !== false ? "on" : "off",
          tabCompletion: isAutocompleteEnabled !== false ? "on" : "off",
          snippetSuggestions: isAutocompleteEnabled !== false ? "inline" : "none",
        }}
        beforeMount={handleEditorWillMount}
        onMount={handleEditorDidMount}
        onChange={handleChange}
      />
      {/* Python language service */}
      {monaco && editorRef.current && (
        <PythonLanguageService
          monaco={monaco}
          editor={editorRef.current}
          code={activeFileContent}
          enabled={isPythonEnabled}
        />
      )}
      {/* Lua language service */}
      {monaco && editorRef.current && (
        <LuaLanguageService
          monaco={monaco}
          editor={editorRef.current}
          code={activeFileContent}
          enabled={isLuaEnabled}
        />
      )}
      {/* C language service */}
      {monaco && editorRef.current && (
        <CLanguageService monaco={monaco} editor={editorRef.current} code={activeFileContent} enabled={isCEnabled} />
      )}
      {/* C++ language service */}
      {monaco && editorRef.current && (
        <CppLanguageService
          monaco={monaco}
          editor={editorRef.current}
          code={activeFileContent}
          enabled={isCppEnabled}
        />
      )}
      {/* XML language service */}
      {monaco && editorRef.current && (
        <XMLLanguageService
          monaco={monaco}
          editor={editorRef.current}
          code={activeFileContent}
          enabled={isXMLEnabled}
        />
      )}
      {/* YAML language service */}
      {monaco && editorRef.current && (
        <YAMLLanguageService
          monaco={monaco}
          editor={editorRef.current}
          code={activeFileContent}
          enabled={isYAMLEnabled}
        />
      )}
      {/* Add drag handle for resizing */}
      <div
        className={cn(
          "h-1 cursor-ns-resize flex items-center justify-center border-t",
          isDarkTheme ? "bg-gray-800" : "bg-gray-200",
        )}
        onMouseDown={(e) => handleCodeEditorResize(e, e.clientY)}
      >
        <GripVertical className={cn("h-3 w-3", isDarkTheme ? "text-gray-600" : "text-gray-400")} />
      </div>
    </div>
  )
}
