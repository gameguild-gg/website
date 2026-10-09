"use client"

import { useEffect } from "react"
import type { editor } from "monaco-editor"

interface XMLSyntaxHighlighterProps {
  monaco: typeof import("monaco-editor") | null
  editor: editor.IStandaloneCodeEditor | null
}

export function XMLSyntaxHighlighter({ monaco, editor }: XMLSyntaxHighlighterProps) {
  useEffect(() => {
    if (!monaco || !editor) return

    // Register XML language if not already registered
    const languages = monaco.languages.getLanguages()
    const xmlLanguage = languages.find((lang) => lang.id === "xml")

    if (!xmlLanguage) {
      console.log("Registering XML language")

      monaco.languages.register({ id: "xml" })

      monaco.languages.setMonarchTokensProvider("xml", {
        tokenizer: {
          root: [
            [/<\?xml[^\n]*\?>/, "metatag.xml"], // XML declaration
            [/<!DOCTYPE[^\n]*?>/, "metatag.xml"], // DOCTYPE declaration
            [/<!--/, { token: "comment", next: "@comment" }],
            [/<!\[CDATA\[/, { token: "comment", next: "@cdata" }], // CDATA sections (may span lines)

            [/<([a-zA-Z][a-zA-Z0-9:\-.]*)(\s+[^>]*)?>/, { token: "tag", bracket: "@open", next: "@tagContent" }], // Opening tags
            [/<\/([a-zA-Z][a-zA-Z0-9:\-.]*)>/, { token: "tag", bracket: "@close" }], // Closing tags

            [/&[a-zA-Z0-9]+;/, "string.escape"], // Entity references

            [/=/, "operator"], // Operators
            [/"[^"\n]*"/, "string"], // Double-quoted string
            [/'[^'\n]*'/, "string"], // Single-quoted string

            [/([a-zA-Z_][a-zA-Z0-9_\-:]*)/, "attribute.name"], // Attribute names
            [/[ \t\r\n]+/, "white"], // Whitespace
            [/[^<&\s=]+/, "attribute.value"], // Unquoted attribute values
          ],

          comment: [
            // HTML spec: both `-->` and the error-tolerant `--!>` close a comment.
            [/--!?>/, { token: "comment", next: "@pop" }],
            [/[^-]+/, "comment"],
            [/-/, "comment"],
          ],

          cdata: [
            [/\]\]>/, { token: "comment", next: "@pop" }],
            [/[^\]]+/, "comment"],
            [/\]/, "comment"],
          ],

          tagContent: [
            [/([a-zA-Z_][a-zA-Z0-9_\-:]*)/, "attribute.name"], // Attribute names
            [/=/, "operator"], // Operators
            [/"([^"\\]|\\.)*"/, "string"], // Double-quoted string
            [/'([^'\\]|\\.)*'/, "string"], // Single-quoted string
            [/&[a-zA-Z0-9]+;/, "string.escape"], // Entity references
            [/(\/>)/, { token: "tag", bracket: "@close", next: "@pop" }], // Self-closing tag
            [/>/, { token: "tag", bracket: "@close", next: "@pop" }], // Closing of opening tag
            [/[\s\t\r\n]+/, "white"], // Whitespace
          ],
        },
      })
    }

    // Set the language for the current model if it's XML
    const model = editor.getModel()
    if (model && model.getLanguageId() === "xml") {
      monaco.editor.setModelLanguage(model, "xml")
    }

    return () => {
      // Cleanup if needed
    }
  }, [monaco, editor])

  return null
}
