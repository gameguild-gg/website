import type React from "react"
import { useState } from "react"
import { Button } from "@/components/ui/button"
import { Textarea } from "@/components/ui/textarea"
import { Prism as SyntaxHighlighter } from "react-syntax-highlighter"
import { vscDarkPlus } from "react-syntax-highlighter/dist/esm/styles/prism"

interface MarkdownCodeActivityProps {
  title: string
  code: string
  language: string
  expectedOutput: string
}

export function MarkdownCodeActivity({
                                       title,
                                       code: initialCode,
                                       language,
                                       expectedOutput,
                                     }: MarkdownCodeActivityProps) {
  const [code, setCode] = useState(initialCode)
  const [output, setOutput] = useState("")
  const [isCorrect, setIsCorrect] = useState<boolean | null>(null)

  const handleCodeChange = (event: React.ChangeEvent<HTMLTextAreaElement>) => {
    setCode(event.target.value)
  }

  const handleRun = () => {
    try {
      // This is a simple evaluation. In a real-world scenario, you'd want to use a safer method.
      const result = eval(code)
      setOutput(String(result))
      setIsCorrect(String(result).trim() === expectedOutput.trim())
    } catch (error) {
      setOutput(`Error: ${error}`)
      setIsCorrect(false)
    }
  }

  return (
    <div className="border rounded-lg p-4 my-4 bg-gray-50">
      <h3 className="text-lg font-semibold mb-2">{title}</h3>
      <SyntaxHighlighter language={language} style={vscDarkPlus} className="mb-4">
        {code}
      </SyntaxHighlighter>
      <Textarea value={code} onChange={handleCodeChange} className="font-mono text-sm mb-4" rows={5} />
      <Button onClick={handleRun} className="mb-4">
        Run Code
      </Button>
      {output && (
        <div className="mb-4">
          <h4 className="font-semibold">Output:</h4>
          <pre className="bg-gray-200 p-2 rounded">{output}</pre>
        </div>
      )}
      {isCorrect !== null && (
        <div className={`p-2 rounded ${isCorrect ? "bg-green-100 text-green-800" : "bg-red-100 text-red-800"}`}>
          {isCorrect ? "Correct output!" : "Incorrect output. Try again!"}
        </div>
      )}
    </div>
  )
}

