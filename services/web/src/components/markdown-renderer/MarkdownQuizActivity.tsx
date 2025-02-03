import { useState } from "react"
import { Button } from "@/components/ui/button"
import { Checkbox } from "@/components/ui/checkbox"

interface QuizProps {
  title: string
  question: string
  options: string[]
  answers: string[]
}

export function MarkdownQuizActivity({ title, question, options, answers }: QuizProps) {
  const [selectedOptions, setSelectedOptions] = useState<string[]>([])
  const [submitted, setSubmitted] = useState(false)

  const handleOptionChange = (option: string) => {
    setSelectedOptions((prev) => (prev.includes(option) ? prev.filter((item) => item !== option) : [...prev, option]))
  }

  const handleSubmit = () => {
    setSubmitted(true)
  }

  const isCorrect = JSON.stringify(selectedOptions.sort()) === JSON.stringify(answers.sort())

  return (
    <div className="border rounded-lg p-4 my-4 bg-gray-50">
      <h3 className="text-lg font-semibold mb-2">{title}</h3>
      <p className="mb-4">{question}</p>
      <div className="space-y-2">
        {options.map((option, index) => (
          <div key={index} className="flex items-center space-x-2">
            <Checkbox
              id={`option-${index}`}
              checked={selectedOptions.includes(option)}
              onCheckedChange={() => handleOptionChange(option)}
              disabled={submitted}
            />
            <label
              htmlFor={`option-${index}`}
              className="text-sm font-medium leading-none peer-disabled:cursor-not-allowed peer-disabled:opacity-70"
            >
              {option}
            </label>
          </div>
        ))}
      </div>
      {!submitted && (
        <Button onClick={handleSubmit} className="mt-4">
          Submit
        </Button>
      )}
      {submitted && (
        <div className={`mt-4 p-2 rounded ${isCorrect ? "bg-green-100 text-green-800" : "bg-red-100 text-red-800"}`}>
          {isCorrect ? "Correct!" : "Incorrect. Try again!"}
        </div>
      )}
    </div>
  )
}

