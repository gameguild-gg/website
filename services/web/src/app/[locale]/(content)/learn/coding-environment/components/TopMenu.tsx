import { Sun, Moon, ZapOff, Play, PlayCircle, Undo, Redo, Settings, SplitSquareVertical } from "lucide-react"
import FileMenu from "./FileMenu"

interface CodeFile {
  name: string
  language: string
  content: string
}

const languageMap = {
  js: "javascript",
  ts: "typescript",
  py: "python",
  rb: "ruby",
  lua: "lua",
}

interface TopMenuProps {
  mode: "light" | "dark" | "high-contrast"
  setMode: (mode: "light" | "dark" | "high-contrast") => void
  onRunFile: () => void
  onRunApplication: (fileName: string) => void
  onNewFile: (name: string, language: string) => void
  onRenameFile: (oldName: string, newName: string) => void
  onDeleteFile: (name: string) => void
  onImportFile: (file: File) => void
  onExportFiles: (exportAll: boolean, singleFileName?: string) => void
  onExportOpenFiles: () => void
  activeFileName: string
  currentFileCount: number
  maxFiles: number
  onUndo: () => void
  onRedo: () => void
  onUndoStart: () => void
  onUndoStop: () => void
  onRedoStart: () => void
  onRedoStop: () => void
  isUndoing: boolean
  isRedoing: boolean
  canUndo: boolean
  canRedo: boolean
  onOpenSettings: () => void
  onRunThis: () => void
  onToggleSplitView: () => void
  isSplitViewActive: boolean
  isRunAnimating: boolean
  isRunThisAnimating: boolean
  isRunAppAnimating: boolean
  activeFile: CodeFile | undefined
  onReset: () => void
  onImportFolder: () => void
}

export default function TopMenu({
  mode,
  setMode,
  onRunFile,
  onRunApplication,
  onNewFile,
  onRenameFile,
  onDeleteFile,
  onImportFile,
  onExportFiles,
  onExportOpenFiles,
  activeFileName,
  currentFileCount,
  maxFiles,
  onUndo,
  onRedo,
  onUndoStart,
  onUndoStop,
  onRedoStart,
  onRedoStop,
  isUndoing,
  isRedoing,
  canUndo,
  canRedo,
  onOpenSettings,
  onRunThis,
  onToggleSplitView,
  isSplitViewActive,
  isRunAnimating,
  isRunThisAnimating,
  isRunAppAnimating,
  activeFile,
  onReset,
  onImportFolder,
}: TopMenuProps) {
  const toggleMode = () => {
    const modes: ("light" | "dark" | "high-contrast")[] = ["light", "dark", "high-contrast"]
    const currentIndex = modes.indexOf(mode)
    const nextMode = modes[(currentIndex + 1) % modes.length]
    setMode(nextMode)
  }

  const getRunButtonLabel = (file: CodeFile | undefined) => {
    if (!file) return "Run"
    const language = languageMap[file.language] || file.language
    switch (language) {
      case "javascript":
        return "Run JS"
      case "html":
        return "Run HTML"
      case "typescript":
        return "Run TS"
      case "python":
        return "Run Python"
      case "ruby":
        return "Run Ruby"
      case "lua":
        return "Run Lua"
      default:
        return "Run"
    }
  }

  return (
    <div
      className={`flex justify-between items-center p-2 ${
        mode === "light"
          ? "bg-white text-gray-800 border-gray-300"
          : mode === "dark"
            ? "bg-gray-800 text-gray-200 border-gray-700"
            : "bg-black text-white border-white"
      } border-b`}
    >
      <div className="flex space-x-4">
        <FileMenu
          mode={mode}
          onNewFile={onNewFile}
          onRenameFile={onRenameFile}
          onDeleteFile={onDeleteFile}
          onImportFile={onImportFile}
          onExportFiles={onExportFiles}
          onExportOpenFiles={onExportOpenFiles}
          activeFileName={activeFileName}
          currentFileCount={currentFileCount}
          maxFiles={maxFiles}
          onReset={onReset}
          onImportFolder={onImportFolder}
        />
        <button
          onClick={onRunFile}
          className={`flex items-center space-x-2 px-3 py-1 rounded transition-colors duration-200 ${
            mode === "light"
              ? "bg-blue-100 text-blue-700 hover:bg-blue-200"
              : mode === "dark"
                ? "bg-blue-700 text-blue-100 hover:bg-blue-600"
                : "bg-yellow-300 text-black hover:bg-yellow-400"
          } ${isRunAnimating ? "animate-slide-right bg-gradient-slide" : ""}`}
        >
          <Play className="w-4 h-4" />
          <span>{getRunButtonLabel(activeFile)}</span>
        </button>
        {/*
        <button
          onClick={onRunThis}
          className={`flex items-center space-x-2 px-3 py-1 rounded transition-colors duration-200 ${
            mode === 'light'
              ? 'bg-green-100 text-green-700 hover:bg-green-200'
              : mode === 'dark'
              ? 'bg-green-700 text-green-100 hover:bg-green-600'
              : 'bg-yellow-300 text-black hover:bg-yellow-400'
          } ${isRunThisAnimating ? 'animate-slide-right bg-gradient-slide' : ''}`}
        >
          <Play className="w-4 h-4" />
          <span>Run this</span>
        </button>
        <button
          onClick={() => onRunApplication(activeFileName)}
          className={`flex items-center space-x-2 px-3 py-1 rounded transition-colors duration-200 ${
            mode === 'light'
              ? 'bg-green-100 text-green-700 hover:bg-green-200'
              : mode === 'dark'
              ? 'bg-green-700 text-green-100 hover:bg-green-600'
              : 'bg-yellow-300 text-black hover:bg-yellow-400'
          } ${isRunAppAnimating ? 'animate-slide-right bg-gradient-slide' : ''}`}
        >
          <PlayCircle className="w-4 h-4" />
          <span>Run app</span>
        </button>
        */}
        <button
          onMouseDown={onUndoStart}
          onMouseUp={onUndoStop}
          onMouseLeave={onUndoStop}
          onClick={onUndo}
          disabled={!canUndo}
          className={`p-2 rounded transition-colors duration-200 ${
            mode === "light"
              ? "bg-gray-200 text-gray-700 hover:bg-gray-300"
              : mode === "dark"
                ? "bg-gray-700 text-gray-200 hover:bg-gray-600"
                : "bg-yellow-300 text-black hover:bg-yellow-400"
          } ${!canUndo && "opacity-50 cursor-not-allowed"}`}
        >
          <Undo className={`w-5 h-5 ${isUndoing ? "animate-pulse" : ""}`} />
        </button>
        <button
          onMouseDown={onRedoStart}
          onMouseUp={onRedoStop}
          onMouseLeave={onRedoStop}
          onClick={onRedo}
          disabled={!canRedo}
          className={`p-2 rounded transition-colors duration-200 ${
            mode === "light"
              ? "bg-gray-200 text-gray-700 hover:bg-gray-300"
              : mode === "dark"
                ? "bg-gray-700 text-gray-200 hover:bg-gray-600"
                : "bg-yellow-300 text-black hover:bg-yellow-400"
          } ${!canRedo && "opacity-50 cursor-not-allowed"}`}
        >
          <Redo className={`w-5 h-5 ${isRedoing ? "animate-pulse" : ""}`} />
        </button>
      </div>
      <div className="flex items-center space-x-2">
        <button
          onClick={onToggleSplitView}
          className={`p-2 rounded-full transition-colors duration-200 ${
            mode === "light"
              ? "bg-gray-200 text-gray-700 hover:bg-gray-300"
              : mode === "dark"
                ? "bg-gray-700 text-gray-200 hover:bg-gray-600"
                : "bg-yellow-300 text-black hover:bg-yellow-400"
          } ${isSplitViewActive ? "ring-2 ring-blue-500" : ""}`}
        >
          <SplitSquareVertical className="w-5 h-5 transform rotate-90" />
        </button>
        <button
          onClick={onOpenSettings}
          className={`p-2 rounded-full transition-colors duration-200 ${
            mode === "light"
              ? "bg-gray-200 text-gray-700 hover:bg-gray-300"
              : mode === "dark"
                ? "bg-gray-700 text-gray-200 hover:bg-gray-600"
                : "bg-yellow-300 text-black hover:bg-yellow-400"
          }`}
        >
          <Settings className="w-5 h-5" />
        </button>
        <button
          onClick={toggleMode}
          className={`p-2 rounded-full transition-colors duration-200 ${
            mode === "light"
              ? "bg-gray-200 text-gray-700 hover:bg-gray-300"
              : mode === "dark"
                ? "bg-gray-700 text-gray-200 hover:bg-gray-600"
                : "bg-yellow-300 text-black hover:bg-yellow-400"
          }`}
        >
          {mode === "light" ? (
            <Sun className="w-5 h-5" />
          ) : mode === "dark" ? (
            <Moon className="w-5 h-5" />
          ) : (
            <ZapOff className="w-5 h-5" />
          )}
        </button>
      </div>
    </div>
  )
}

