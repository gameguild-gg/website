import { Play, PlayCircle } from 'lucide-react'

interface BottomMenuProps {
  activeTab: 'problems' | 'output' | 'testResults' | null
  setActiveTab: (tab: 'problems' | 'output' | 'testResults' | null) => void
  mode: 'light' | 'dark' | 'high-contrast'
  hideTestOutputs: boolean
}

export default function BottomMenu({ activeTab, setActiveTab, mode, hideTestOutputs }: BottomMenuProps) {
  const handleTabClick = (tab: 'problems' | 'output' | 'testResults') => {
    setActiveTab(activeTab === tab ? null : tab)
  }

  return (
    <div className={`flex min-h-[40px] ${
      mode === 'light' ? 'bg-gray-100 text-gray-800 border-gray-300' :
      mode === 'dark' ? 'bg-gray-800 text-gray-200 border-gray-700' :
      'bg-black text-white border-white'
    } border-t`}>
      <button
        onClick={() => handleTabClick('problems')}
        className={`px-4 py-2 ${
          activeTab === 'problems'
            ? mode === 'light' ? 'bg-white border-t-2 border-blue-500 text-blue-600' :
              mode === 'dark' ? 'bg-gray-900 border-t-2 border-blue-400 text-blue-400' :
              'bg-white border-t-2 border-yellow-400 text-black'
            : mode === 'light' ? 'hover:bg-gray-200' :
              mode === 'dark' ? 'hover:bg-gray-700' :
              'hover:bg-gray-800'
        }`}
      >
        Expected - I/O
      </button>
      <button
        onClick={() => handleTabClick('output')}
        className={`px-4 py-2 ${
          activeTab === 'output'
            ? mode === 'light' ? 'bg-white border-t-2 border-blue-500 text-blue-600' :
              mode === 'dark' ? 'bg-gray-900 border-t-2 border-blue-400 text-blue-400' :
              'bg-white border-t-2 border-yellow-400 text-black'
            : mode === 'light' ? 'hover:bg-gray-200' :
              mode === 'dark' ? 'hover:bg-gray-700' :
              'hover:bg-gray-800'
        }`}
      >
        Output
      </button>
      <button
        onClick={() => handleTabClick('testResults')}
        className={`px-4 py-2 ${
          activeTab === 'testResults'
            ? mode === 'light' ? 'bg-white border-t-2 border-blue-500 text-blue-600' :
              mode === 'dark' ? 'bg-gray-900 border-t-2 border-blue-400 text-blue-400' :
              'bg-white border-t-2 border-yellow-400 text-black'
            : mode === 'light' ? 'hover:bg-gray-200' :
              mode === 'dark' ? 'hover:bg-gray-700' :
              'hover:bg-gray-800'
        }`}
      >
        Test Results
      </button>
    </div>
  )
}

