import React from 'react'

interface TestResult {
  expectedOutput: string
  actualOutput: string
  passed: boolean
}

interface TestResultsTabProps {
  testResults: TestResult[]
  mode: 'light' | 'dark' | 'high-contrast'
  hideTestOutputs: boolean
}

export default function TestResultsTab({ testResults, mode, hideTestOutputs }: TestResultsTabProps) {
  const getColorClass = (passed: boolean) => {
    if (mode === 'light') {
      return passed ? 'bg-green-100 text-green-800' : 'bg-red-100 text-red-800'
    } else if (mode === 'dark') {
      return passed ? 'bg-green-800 text-green-100' : 'bg-red-800 text-red-100'
    } else {
      return passed ? 'bg-yellow-300 text-black' : 'bg-yellow-800 text-yellow-100'
    }
  }

  return (
    <div>
      <h2 className={`text-xl font-bold mb-4 ${
        mode === 'light' ? 'text-gray-800' :
        mode === 'dark' ? 'text-gray-200' :
        'text-yellow-300'
      }`}>Test Results</h2>
      {testResults.length > 0 ? (
        <>
          {testResults.map((result, index) => (
            <div key={index} className={`mb-4 p-4 rounded ${getColorClass(result.passed)}`}>
              <div className="flex items-center mb-2">
                <span className={`font-bold mr-2 ${
                  mode === 'light' ? 'text-gray-800' :
                  mode === 'dark' ? 'text-gray-200' :
                  'text-yellow-300'
                }`}>Test #{index + 1}:</span>
                <p className="font-bold">Status: {result.passed ? 'Passed' : 'Failed'}</p>
              </div>
              {!hideTestOutputs && (
                <div className="mb-2">
                  <p className="font-semibold">Expected Output:</p>
                  <pre className="ml-4 whitespace-pre-wrap">{result.expectedOutput}</pre>
                </div>
              )}
              <div className="mb-2">
                <p className="font-semibold">Actual Output:</p>
                <pre className="ml-4 whitespace-pre-wrap">{result.actualOutput}</pre>
              </div>
            </div>
          ))}
          <p className={`mt-4 font-bold ${
            mode === 'light' ? 'text-gray-800' :
            mode === 'dark' ? 'text-gray-200' :
            'text-yellow-300'
          }`}>
            Passed: {testResults.filter(r => r.passed).length} / {testResults.length}
          </p>
        </>
      ) : (
        <p className={
          mode === 'light' ? 'text-gray-600' :
          mode === 'dark' ? 'text-gray-400' :
          'text-yellow-200'
        }>No test results available. Run your code to see the results.</p>
      )}
    </div>
  )
}

