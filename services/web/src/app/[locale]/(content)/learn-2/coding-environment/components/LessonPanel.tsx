import React, { useState, useRef, useCallback, useEffect } from "react"
import ReactMarkdown from "react-markdown"
import type { QuestionBasev1_0_0 } from "@/interface-base/question.base.v1.0.0"
import { Button } from "@/components/ui/button"
import CustomScrollbar from "./CustomScrollbar"
import type { HierarchyBasev1_0_0 } from "@/interface-base/structure.base.v1.0.0"
import { ChevronDown, ChevronRight, FileText, Folder, FileCode, FileJson, FileIcon as FileHtml, FileCodeIcon as FileCss, Coffee, BracesIcon, FileType, Gem, Moon, FileTerminal, Cpu, Hash } from 'lucide-react'
import { Resizable } from "re-resizable"
import type { CodeFile } from "../types/codeEditor"
import { PanelGroup, Panel, PanelResizeHandle } from "react-resizable-panels"

interface LessonPanelProps {
  assignment: QuestionBasev1_0_0
  onReturn: () => void
  onSubmit: () => void
  onReset: () => void
  mode: "light" | "dark" | "high-contrast"
  isSubmitDisabled: boolean
  isSubmitHidden: boolean
  hierarchy: HierarchyBasev1_0_0
  globalCodeFiles: CodeFile[]
  handleOpenFile: (fileName: string) => void
  codeFiles: CodeFile[]
}

interface File {
  name: string
  type: "file" | "folder"
  children?: File[]
}

const FileIcon = ({ file }: { file: File }) => {
  if (file.type === "folder") {
    return <Folder className="w-4 h-4 text-yellow-500 dark:text-yellow-400 high-contrast:text-yellow-300" />
  } else {
    const extension = file.name.split(".").pop()?.toLowerCase()
    switch (extension) {
      case "js":
        return <Coffee className="w-4 h-4 text-yellow-500 dark:text-yellow-400 high-contrast:text-yellow-300" />
      case "ts":
        return <FileCode className="w-4 h-4 text-blue-500 dark:text-blue-400 high-contrast:text-blue-300" />
      case "json":
        return <FileJson className="w-4 h-4 text-green-500 dark:text-green-400 high-contrast:text-green-300" />
      case "html":
        return <FileHtml className="w-4 h-4 text-orange-500 dark:text-orange-400 high-contrast:text-orange-300" />
      case "css":
        return <FileCss className="w-4 h-4 text-blue-500 dark:text-blue-400 high-contrast:text-blue-300" />
      case "py":
        return <FileType className="w-4 h-4 text-green-600 dark:text-green-400 high-contrast:text-green-300" />
      case "rb":
        return <Gem className="w-4 h-4 text-red-500 dark:text-red-400 high-contrast:text-red-300" />
      case "lua":
        return <Moon className="w-4 h-4 text-blue-400 dark:text-blue-300 high-contrast:text-blue-200" />
      case "c":
      case "cpp":
        return <FileTerminal className="w-4 h-4 text-purple-500 dark:text-purple-400 high-contrast:text-purple-300" />
      case "rs":
        return <Cpu className="w-4 h-4 text-orange-600 dark:text-orange-400 high-contrast:text-orange-300" />
      case "java":
        return <Coffee className="w-4 h-4 text-brown-500 dark:text-brown-400 high-contrast:text-brown-300" />
      case "cs":
        return <Hash className="w-4 h-4 text-purple-500 dark:text-purple-400 high-contrast:text-purple-300" />
      default:
        return <FileText className="w-4 h-4 text-gray-500 dark:text-gray-400 high-contrast:text-gray-300" />
    }
  }
}

const FileExplorer = ({
  files,
  mode,
  handleOpenFile,
  globalCodeFiles,
  codeFiles,
}: {
  files: File[]
  mode: "light" | "dark" | "high-contrast"
  handleOpenFile: (fileName: string) => void
  globalCodeFiles: CodeFile[]
  codeFiles: CodeFile[]
}) => {
  const [expandedFolders, setExpandedFolders] = useState<string[]>([])

  const handleToggleFolder = (folderPath: string) => {
    setExpandedFolders((prev) =>
      prev.includes(folderPath) ? prev.filter((path) => path !== folderPath) : [...prev, folderPath],
    )
  }

  const getFileStateClasses = (file: File) => {
    const isOpen = codeFiles.some((f) => f.name.split("/").pop() === file.name.split("/").pop())
    const isModified = globalCodeFiles.some(
      (f) => f.name.split("/").pop() === file.name.split("/").pop() && f.content !== "",
    )
    const baseClasses = "transition-all duration-200 ease-in-out"

    if (isOpen) {
      return `${baseClasses} font-semibold text-blue-500 bg-blue-100 dark:bg-blue-900 dark:text-blue-300 high-contrast:bg-yellow-900 high-contrast:text-yellow-300 rounded-md`
    } else if (isModified) {
      return `${baseClasses} font-medium text-green-600 dark:text-green-400 high-contrast:text-yellow-400`
    } else {
      return `${baseClasses} text-gray-700 dark:text-gray-300 high-contrast:text-gray-400`
    }
  }

  const renderFile = (file: File, path: string) => {
    const isFolder = file.type === "folder"
    const isExpanded = expandedFolders.includes(path)
    const stateClasses = getFileStateClasses(file)

    return (
      <div key={path} className="relative">
        <div
          className={`flex items-center p-2 cursor-pointer hover:bg-gray-100 dark:hover:bg-gray-700 high-contrast:hover:bg-gray-800 ${stateClasses}`}
          onClick={() => {
            if (isFolder) {
              handleToggleFolder(path)
            } else {
              handleOpenFile(path)
            }
          }}
        >
          <FileIcon file={file} />
          <span className="ml-2 text-sm font-medium">{file.name}</span>
          {isFolder && (
            <ChevronDown className={`ml-auto transition-transform duration-200 ${isExpanded ? "rotate-180" : ""}`} />
          )}
        </div>
        {isFolder && isExpanded && (
          <div className="ml-4 border-l border-gray-200 dark:border-gray-600 high-contrast:border-gray-700">
            {file.children?.map((child) => renderFile(child, `${path}/${child.name}`))}
          </div>
        )}
      </div>
    )
  }

  return (
    <div className="p-2 overflow-y-auto h-full scrollbar-thin scrollbar-thumb-gray-400 scrollbar-track-gray-200 dark:scrollbar-thumb-gray-600 dark:scrollbar-track-gray-800 high-contrast:scrollbar-thumb-yellow-500 high-contrast:scrollbar-track-gray-900">
      {files.map((file) => renderFile(file, file.name))}
    </div>
  )
}

export default function LessonPanel({
  assignment,
  onReturn,
  onSubmit,
  onReset,
  mode,
  isSubmitDisabled,
  isSubmitHidden,
  hierarchy,
  globalCodeFiles,
  handleOpenFile,
  codeFiles,
}: LessonPanelProps) {
  const [descriptionHeight, setDescriptionHeight] = useState<number>(50)

  const fileStructure = globalCodeFiles.reduce((acc, file) => {
    const pathParts = file.name ? file.name.split("/") : []
    let currentLevel = acc
    for (let i = 0; i < pathParts.length - 1; i++) {
      const part = pathParts[i]
      if (!currentLevel.find((f: File) => f.name === part)) {
        currentLevel.push({ name: part, type: "folder", children: [] })
      }
      currentLevel = currentLevel.find((f: File) => f.name === part)?.children || []
    }
    currentLevel.push({ name: pathParts[pathParts.length - 1], type: "file" })
    return acc
  }, [] as File[])

  return (
    <div
      className={`flex flex-col h-full ${
        mode === "light"
          ? "bg-white text-gray-800 border-gray-300"
          : mode === "dark"
            ? "bg-gray-900 text-gray-100 border-gray-700"
            : "bg-black text-yellow-300 border-yellow-300"
      }`}
    >
      <div className="p-4 border-b flex justify-between" style={{ flexShrink: 0 }}>
        <Button
          onClick={onReturn}
          variant="outline"
          size="sm"
          className={`
          ${
            mode === "light"
              ? "bg-gray-200 text-gray-800 hover:bg-gray-300"
              : mode === "dark"
                ? "bg-gray-700 text-gray-100 hover:bg-gray-600"
                : "bg-yellow-300 text-black hover:bg-yellow-400 border-2 border-yellow-300"
          }
        `}
        >
          Return to List
        </Button>
      </div>
      <div className="flex-grow overflow-hidden">
        <PanelGroup direction="vertical">
          <Panel minSize={10}>
            <div className="h-full overflow-hidden">
              <CustomScrollbar mode={mode} className="h-full">
                <div className={`p-4 max-h-[calc(100%-8px)] overflow-y-auto custom-scrollbar ${
                  mode === "light"
                    ? "scrollbar-thumb-gray-400 scrollbar-track-gray-200"
                    : mode === "dark"
                      ? "scrollbar-thumb-gray-600 scrollbar-track-gray-800"
                      : "scrollbar-thumb-yellow-500 scrollbar-track-gray-900"
                }`}>
                  <h1 className="text-2xl font-bold mb-4">{assignment.title}</h1>
                  <ReactMarkdown
                    className={`prose max-w-none ${
                      mode === "light" ? "prose-gray" : mode === "dark" ? "prose-invert" : "prose-yellow prose-invert"
                    }`}
                  >
                    {assignment.description}
                  </ReactMarkdown>
                </div>
              </CustomScrollbar>
            </div>
          </Panel>
          <PanelResizeHandle
            className={`h-2 transition-colors ${
              mode === "light"
                ? "bg-gray-300 hover:bg-gray-400"
                : mode === "dark"
                  ? "bg-gray-700 hover:bg-gray-600"
                  : "bg-yellow-500 hover:bg-yellow-400"
            }`}
          />
          <Panel minSize={10}>
            <div className="h-full overflow-hidden border-t border-gray-200 dark:border-gray-600">
              <CustomScrollbar mode={mode} className="h-full">
                <FileExplorer
                  files={fileStructure}
                  mode={mode}
                  handleOpenFile={handleOpenFile}
                  globalCodeFiles={globalCodeFiles}
                  codeFiles={codeFiles}
                />
              </CustomScrollbar>
            </div>
          </Panel>
        </PanelGroup>
      </div>
      <div className="p-4 border-t" style={{ flexShrink: 0 }}>
        {!isSubmitHidden && (
          <Button
            onClick={onSubmit}
            className={`w-full ${
              mode === "light"
                ? "bg-blue-500 hover:bg-blue-600 text-white"
                : mode === "dark"
                  ? "bg-blue-600 hover:bg-blue-700 text-white"
                  : "bg-yellow-300 hover:bg-yellow-400 text-black"
            }`}
            disabled={isSubmitDisabled}
          >
            Submit
          </Button>
        )}
      </div>
    </div>
  )
}

