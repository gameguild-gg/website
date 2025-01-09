'use client'

import { Button } from "@/components/ui/button"

export default function FinalTab({ courseData }) {
  const handlePublish = async () => {
    try {
      const response = await fetch('/api/publish-course', {
        method: 'POST',
        headers: {
          'Content-Type': 'application/json',
        },
        body: JSON.stringify(courseData),
      })
      if (response.ok) {
        alert('Course published successfully!')
      } else {
        throw new Error('Failed to publish course')
      }
    } catch (error) {
      console.error('Error publishing course:', error)
      alert('Failed to publish course. Please try again.')
    }
  }

  const handleSend = async () => {
    try {
      const response = await fetch('/api/send-course', {
        method: 'POST',
        headers: {
          'Content-Type': 'application/json',
        },
        body: JSON.stringify(courseData),
      })
      if (response.ok) {
        alert('Course sent successfully!')
      } else {
        throw new Error('Failed to send course')
      }
    } catch (error) {
      console.error('Error sending course:', error)
      alert('Failed to send course. Please try again.')
    }
  }

  const handleDownload = () => {
    const jsonString = JSON.stringify(courseData, null, 2)
    const blob = new Blob([jsonString], { type: 'application/json' })
    const url = URL.createObjectURL(blob)
    const a = document.createElement('a')
    a.href = url
    a.download = 'course_data.json'
    document.body.appendChild(a)
    a.click()
    document.body.removeChild(a)
    URL.revokeObjectURL(url)
  }

  return (
    <div className="space-y-4">
      <h2 className="text-2xl font-bold">Course Summary</h2>
      <pre className="bg-gray-100 p-4 rounded-md overflow-auto max-h-[400px]">
        {JSON.stringify(courseData, null, 2)}
      </pre>
      <div className="flex flex-wrap gap-4">
        <Button onClick={handlePublish}>Publish Course</Button>
        <Button onClick={handleSend}>Send Course</Button>
        <Button onClick={handleDownload}>Download Course</Button>
      </div>
    </div>
  )
}

