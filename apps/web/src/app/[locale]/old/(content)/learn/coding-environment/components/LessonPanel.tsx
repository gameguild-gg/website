import React, { useState, useRef, useCallback, useEffect } from "react";
import ReactMarkdown from "react-markdown";
import type { QuestionBasev1_0_0 } from "@/lib/interface-base/question.base.v1.0.0";
import { Button } from "@/components/learn/ui/button";
import CustomScrollbar from "./CustomScrollbar";
import type { HierarchyBasev1_0_0 } from "@/lib/interface-base/structure.base.v1.0.0";
import { ChevronDown, FileText, Folder} from 'lucide-react';
//import { DiCss3, DiHtml5, DiJava, DiPerl, DiRuby, DiScriptcs } from "react-icons/di";
//import { SiC, SiCashapp, SiCplusplus, SiCss3, SiHtml5, SiJavascript, SiJpeg, SiJson, SiLua, SiOracle, SiPerl, SiPython, SiRuby, SiRust, SiSvg, SiTypescript, SiWebassembly, SiXml } from "react-icons/si";
import { Resizable } from "re-resizable";
import type { CodeFile } from "../types/codeEditor";
import { PanelGroup, Panel, PanelResizeHandle } from "react-resizable-panels";
import { SidebarGroupLabel } from "@/components/learn/ui/sidebar";

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

const FileIcon = ({ file, mode }: { file: File; mode: string }) => {

  if (file.type === "folder") {
    return <Folder className={`w-4 h-4 ${mode === "light" ? "text-gray-700" : mode === "dark" ? "text-gray-200" : "text-yellow-300"}`} />
  } else {
    const extension = file.name.split(".").pop()?.toLowerCase()
    switch (extension) {
      case "js":
        return <FileText className={`w-4 h-4 ${mode === "light" ? "text-yellow-400" : mode === "dark" ? "text-yellow-400" : "text-yellow-400"}`} />
      case "ts":
        return <FileText className={`w-4 h-4 ${mode === "light" ? "text-blue-400" : mode === "dark" ? "text-blue-400" : "text-yellow-400"}`} />
      case "json":
        return <FileText className={`w-4 h-4 ${mode === "light" ? "text-green-400" : mode === "dark" ? "text-green-400" : "text-yellow-400"}`} />
      case "xml":
        return <FileText className={`w-4 h-4 ${mode === "light" ? "text-green-400" : mode === "dark" ? "text-green-400" : "text-yellow-400"}`} />
      case "svg":
        return <FileText className={`w-4 h-4 ${mode === "light" ? "text-green-400" : mode === "dark" ? "text-green-400" : "text-yellow-400"}`} />
      case "jpeg":
      case "jpg":
        return <FileText className={`w-4 h-4 ${mode === "light" ? "text-green-400" : mode === "dark" ? "text-green-400" : "text-yellow-400"}`} />
      case "html":
        return <FileText className={`w-4 h-4 ${mode === "light" ? "text-green-400" : mode === "dark" ? "text-green-400" : "text-yellow-400"}`} />
      case "css":
        return <FileText className={`w-4 h-4 ${mode === "light" ? "text-green-400" : mode === "dark" ? "text-green-400" : "text-yellow-400"}`} />
      case "py":
        return <FileText className={`w-4 h-4 ${mode === "light" ? "text-blue-400" : mode === "dark" ? "text-blue-400" : "text-yellow-400"}`} />
      case "rb":
        return <FileText className={`w-4 h-4 ${mode === "light" ? "text-red-500" : mode === "dark" ? "text-red-400" : "text-yellow-400"}`} />
      case "lua":
        return <FileText className={`w-4 h-4 ${mode === "light" ? "text-blue-400" : mode === "dark" ? "text-blue-400" : "text-yellow-400"}`} />
      case "c":
        return <FileText className={`w-4 h-4 ${mode === "light" ? "text-gray-400" : mode === "dark" ? "text-gray-400" : "text-yellow-400"}`} />
      case "cpp":
        return <FileText className={`w-4 h-4 ${mode === "light" ? "text-gray-400" : mode === "dark" ? "text-gray-400" : "text-yellow-400"}`} />
      case "rs":
        return <FileText className={`w-4 h-4 ${mode === "light" ? "text-amber-600" : mode === "dark" ? "text-amber-600" : "text-yellow-400"}`} />
      case "java":
        return <FileText className={`w-4 h-4 ${mode === "light" ? "text-blue-700" : mode === "dark" ? "text-blue-400" : "text-yellow-400"}`} />
      case "cs":
        return <FileText className={`w-4 h-4 ${mode === "light" ? "text-purple-600" : mode === "dark" ? "text-purple-400" : "text-yellow-400"}`} />
      case "perl":
        return <FileText className={`w-4 h-4 ${mode === "light" ? "text-amber-400" : mode === "dark" ? "text-amber-400" : "text-yellow-400"}`} />
      case "wasm":
        return <FileText className={`w-4 h-4 ${mode === "light" ? "text-blue-400" : mode === "dark" ? "text-blue-400" : "text-yellow-400"}`} />
      default:
        return <FileText className={`w-4 h-4 ${mode === "light" ? "text-gray-400" : mode === "dark" ? "text-gray-400" : "text-yellow-400"}`} />
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

  const getFileStateClasses = (file: File, mode: string) => {
    const isOpen = codeFiles.some((f) => f.name.split("/").pop() === file.name.split("/").pop())
    const isModified = globalCodeFiles.some(
      (f) => f.name.split("/").pop() === file.name.split("/").pop() && f.content !== "",
    )
    const baseClasses = "transition-all duration-100 ease-in-out"

    if (isOpen) {
      return `${baseClasses} font-semibold ${mode === "light" ? "text-blue-800 bg-blue-100" : mode === "dark" ? "bg-blue-950 text-blue-100" : "bg-yellow-950 text-yellow-300"} rounded-md`
    } else if (isModified) {
      return `${baseClasses} font-medium ${mode === "light" ? "text-green-800" : mode === "dark" ? "text-green-300" : "text-yellow-400"}`
    } else {
      return `${baseClasses} ${mode === "light" ? "text-gray-700" : mode === "dark" ? "text-gray-300" : "text-gray-400"}`
    }
  }

  const renderFile = (file: File, path: string) => {
    const isFolder = file.type === "folder"
    const isExpanded = expandedFolders.includes(path)
    const stateClasses = getFileStateClasses(file,mode)

    return (
      <div key={path} className="relative">
        <div
          className={`flex items-center m-1 p-1 cursor-pointer rounded-md hover:${mode === "light" ? "bg-blue-200" : mode === "dark" ? "bg-blue-900" : "bg-yellow-900"} ${stateClasses}`}
          onClick={() => {
            if (isFolder) {
              handleToggleFolder(path)
            } else {
              handleOpenFile(path)
            }
          }}
        >
          <FileIcon file={file} mode={mode} />
          <span className="ml-2 text-sm font-medium">{file.name}</span>
          {isFolder && (
            <ChevronDown className={`ml-auto transition-transform duration-200 ${isExpanded ? "rotate-180" : ""}`} />
          )}
        </div>
        {isFolder && isExpanded && (
          <div className={`ml-2 border-l-2 ${mode === "light" ? "border-gray-200" : mode === "dark" ? "border-gray-600" : "border-gray-700"}`}>
            {file.children?.map((child) => renderFile(child, `${path}/${child.name}`))}
          </div>
        )}
      </div>
    )
  }

  return (
    <div className={`p-2 overflow-y-auto h-full scrollbar-thin ${mode === "light" ? "scrollbar-thumb-gray-400 scrollbar-track-gray-200" : mode === "dark" ? "scrollbar-thumb-gray-600 scrollbar-track-gray-800" : "scrollbar-thumb-yellow-500 scrollbar-track-gray-900"}`}>
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
            ? "bg-gray-900 text-gray-200 border-gray-700"
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
                  ? "bg-blue-700 hover:bg-blue-800 text-white"
                  : "bg-yellow-500 hover:bg-yellow-200 text-black"
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