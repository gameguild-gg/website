import { useState } from 'react'
import { Input } from "@/components/ui/input"
import { Button } from "@/components/ui/button"
import { useCourseStore } from '@/store/courseStore'

export function UniqueUrlInput() {
  const { uniqueUrl, setUniqueUrl } = useCourseStore()
  const [isAvailable, setIsAvailable] = useState<boolean | null>(null)
  const [isChecking, setIsChecking] = useState(false)

  const handleInputChange = (e: React.ChangeEvent<HTMLInputElement>) => {
    setUniqueUrl(e.target.value)
    setIsAvailable(null)
  }

  const checkAvailability = async () => {
    setIsChecking(true)
    // Simulating an API call to check URL availability
    await new Promise(resolve => setTimeout(resolve, 1000))
    setIsAvailable(Math.random() > 0.5) // Random availability for demonstration
    setIsChecking(false)
  }

  return (
    <div className="flex space-x-2">
      <Input
        name="uniqueUrl"
        placeholder="Unique URL"
        value={uniqueUrl}
        onChange={handleInputChange}
        className={isAvailable === true ? 'border-green-500' : isAvailable === false ? 'border-red-500' : ''}
        required
      />
      <Button onClick={checkAvailability} disabled={isChecking}>
        {isChecking ? 'Checking...' : 'Verify'}
      </Button>
    </div>
  )
}

