import React from "react"

export default function Loading() {
  return (
    <div className="flex items-center justify-center min-h-screen bg-background text-foreground">
      <div className="animate-spin rounded-full h-32 w-32 border-t-2 border-b-2 border-primary"></div>
    </div>
  )
}

