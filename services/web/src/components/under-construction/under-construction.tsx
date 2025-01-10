import { AlertTriangle } from 'lucide-react'
import { Button } from "@/components/ui/button"
import Link from 'next/link'

interface UnderConstructionProps {
  pageName: string
}

export default function UnderConstruction({ pageName }: UnderConstructionProps) {
  return (
    <div className="flex flex-col items-center justify-center min-h-screen bg-background text-foreground p-4">
      <AlertTriangle className="w-16 h-16 text-yellow-500 mb-4" />
      <h1 className="text-4xl font-bold mb-2 text-center">Under Construction</h1>
      <p className="text-xl mb-4 text-center">The {pageName} page is coming soon!</p>
      <p className="text-lg mb-8 text-center max-w-md">We're working hard to bring you an amazing experience. Please check back later.</p>
      <Button asChild>
        <Link href="/">
          Return to Home
        </Link>
      </Button>
    </div>
  )
}

