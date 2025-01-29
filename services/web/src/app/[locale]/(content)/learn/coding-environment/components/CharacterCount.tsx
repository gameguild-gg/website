import React, { useState } from "react"
import { Eye, EyeOff } from "lucide-react"

interface CharacterCountProps {
  currentFileChars: number
  maxFileChars: number
  totalChars: number
  maxTotalChars: number
  fileCount: number
  maxFiles: number
  mode: "light" | "dark" | "high-contrast"
}

export function CharacterCount({
  currentFileChars,
  maxFileChars,
  totalChars,
  maxTotalChars,
  fileCount,
  maxFiles,
  mode,
}: CharacterCountProps) {
  const [isVisible, setIsVisible] = useState(true)
  const toggleVisibility = () => setIsVisible(!isVisible)

  const getColorClass = (current: number, max: number) => {
    const percentage = (current / max) * 100
    if (current > max) return "text-red-500"
    if (percentage >= 90) return "text-orange-600"
    if (percentage >= 75) return "text-orange-400"
    if (percentage >= 50) return "text-yellow-400"
    return "text-green-500"
  }

  return (
    <div
      className={`flex flex-wrap items-center space-x-2 text-sm ${
        mode === "light"
          ? "bg-gray-100 text-gray-800"
          : mode === "dark"
            ? "bg-gray-800 text-gray-200"
            : "bg-black text-white"
      } p-2`}
    >
      <button onClick={toggleVisibility}>{isVisible ? <EyeOff size={16} /> : <Eye size={16} />}</button>
      {isVisible && (
        <>
          <span>
            Chars this: <span className={getColorClass(currentFileChars, maxFileChars)}>{currentFileChars}</span> /{" "}
            {maxFileChars}
          </span>
          <span>|</span>
          <span>
            Chars total: <span className={getColorClass(totalChars, maxTotalChars)}>{totalChars}</span> /{" "}
            {maxTotalChars}
          </span>
          <span>|</span>
          <span>
            Files: <span className={getColorClass(fileCount, maxFiles)}>{fileCount}</span> / {maxFiles}
          </span>
        </>
      )}
    </div>
  )
}

