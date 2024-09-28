'use client'

import { useState } from 'react'
import { Button } from "@/components/ui/button"
import { ScrollArea } from "@/components/ui/scroll-area"
import { Checkbox } from "@/components/ui/checkbox"
import { PlayCircle, FileText, ChevronLeft, ChevronRight } from 'lucide-react'

const chapters = [
  { id: 1, title: 'Introduction to React', type: 'video', content: 'https://example.com/intro-to-react.mp4' },
  { id: 2, title: 'Development environment', type: 'text', content: 'To get started with React development, you\'ll need to set up your environment. First, install Node.js and npm. Then, you can use Create React App to quickly set up a new React project. Run the following commands in your terminal:\n\n```\nnpx create-react-app my-react-app\ncd my-react-app\nnpm start\n```\n\nThis will create a new React application and start a development server.' },
  { id: 3, title: 'Components and Props', type: 'video', content: 'https://example.com/components-and-props.mp4' },
  { id: 4, title: 'State and Lifecycle', type: 'video', content: 'https://example.com/state-and-lifecycle.mp4' },
  { id: 5, title: 'Handling Events in React', type: 'text', content: 'React allows you to handle events similar to handling events on DOM elements. Here\'s an example of how to handle a button click event in React:\n\n```jsx\nfunction Button() {\n  const handleClick = () => {\n    console.log(\'Button clicked!\');\n  }\n\n  return (\n    <button onClick={handleClick}>\n      Click me\n    </button>\n  );\n}\n```\n\nIn this example, we define a `handleClick` function and pass it to the `onClick` prop of the button element.' },
]

type Props = {
  params: {
    slug: string;
    chapter: string;
  };
};

export default function Component({params: {slug, chapter}}: Readonly<Props>) {
  const [currentChapter, setCurrentChapter] = useState(chapters[0])
  const [completedChapters, setCompletedChapters] = useState<number[]>([])

  // fetch chapter content here

  const handleChapterClick = (chapter: typeof chapters[0]) => {
    setCurrentChapter(chapter)
  }

  const handleCompletedToggle = (chapterId: number) => {
    setCompletedChapters(prev => 
      prev.includes(chapterId) 
        ? prev.filter(id => id !== chapterId)
        : [...prev, chapterId]
    )
  }

  const handleNextChapter = () => {
    const currentIndex = chapters.findIndex(chapter => chapter.id === currentChapter.id)
    if (currentIndex < chapters.length - 1) {
      setCurrentChapter(chapters[currentIndex + 1])
    }
  }

  const handlePreviousChapter = () => {
    const currentIndex = chapters.findIndex(chapter => chapter.id === currentChapter.id)
    if (currentIndex > 0) {
      setCurrentChapter(chapters[currentIndex - 1])
    }
  }

  return (
    <div className="flex container mx-auto px-4 py-8">
      <aside className="w-80 bg-muted border-r">
        <ScrollArea className="h-full">
          <nav className="p-4">
            <h2 className="text-xl font-semibold mb-4">Chapters</h2>
            <ul>
              {chapters.map((chapter) => (
                <li key={chapter.id} className="mb-2">
                  <div className="flex items-center">
                    <Checkbox 
                      id={`chapter-${chapter.id}`}
                      checked={completedChapters.includes(chapter.id)}
                      onCheckedChange={() => handleCompletedToggle(chapter.id)}
                      className="mr-2"
                    />
                    <button
                      onClick={() => handleChapterClick(chapter)}
                      className={`flex items-center text-sm ${currentChapter.id === chapter.id ? 'font-bold' : ''}`}
                    >
                      {chapter.type === 'video' ? (
                        <PlayCircle className="mr-2 h-4 w-4" />
                      ) : (
                        <FileText className="mr-2 h-4 w-4" />
                      )}
                      {chapter.title}
                    </button>
                  </div>
                </li>
              ))}
            </ul>
          </nav>
        </ScrollArea>
      </aside>
      <main className="flex-1 p-6 overflow-auto">
        <h1 className="text-2xl font-bold mb-4">{currentChapter.title}</h1>
        {currentChapter.type === 'video' ? (
          <div className="aspect-w-16 aspect-h-9 mb-4">
            (Video Here)
          </div>
          /*
          <div className="aspect-w-16 aspect-h-9 mb-4">
            <iframe
              src={currentChapter.content}
              allow="accelerometer; autoplay; clipboard-write; encrypted-media; gyroscope; picture-in-picture"
              allowFullScreen
              className="w-full h-full"
            ></iframe>
          </div>
          */
        ) : (
          <div className="prose max-w-none">
            {currentChapter.content.split('\n').map((paragraph, index) => (
              <p key={index}>{paragraph}</p>
            ))}
          </div>
        )}
        <div className="flex justify-between mt-6">
          <Button
            onClick={handlePreviousChapter}
            disabled={currentChapter.id === chapters[0].id}
          >
            <ChevronLeft className="mr-2 h-4 w-4" /> Previous
          </Button>
          <Button
            onClick={handleNextChapter}
            disabled={currentChapter.id === chapters[chapters.length - 1].id}
          >
            Next <ChevronRight className="ml-2 h-4 w-4" />
          </Button>
        </div>
      </main>
    </div>
  )
}