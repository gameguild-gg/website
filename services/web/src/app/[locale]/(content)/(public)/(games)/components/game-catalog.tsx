"use client"

import { useState } from "react"
import Image from "next/image"
import { Card } from "@/components/ui/card"
import ControlBar from '@/app/[locale]/(content)/(public)/(games)/components/control-bar';


const games = [
  {
    id: 1,
    title: "Destiny 2",
    image: "/placeholder.svg?height=300&width=200",
    new: true
  },
  {
    id: 2,
    title: "League of Legends",
    image: "/placeholder.svg?height=300&width=200",
  },
  {
    id: 3,
    title: "World of Warcraft",
    image: "/placeholder.svg?height=300&width=200",
  },
  {
    id: 4,
    title: "Hearthstone",
    image: "/placeholder.svg?height=300&width=200",
  },
  {
    id: 5,
    title: "GTA V",
    image: "/placeholder.svg?height=300&width=200",
  },
  {
    id: 6,
    title: "DOOM",
    image: "/placeholder.svg?height=300&width=200",
  },
  {
    id: 7,
    title: "Cyberpunk 2077",
    image: "/placeholder.svg?height=300&width=200",
  },
  {
    id: 8,
    title: "Red Dead Redemption 2",
    image: "/placeholder.svg?height=300&width=200",
  },
  {
    id: 9,
    title: "The Witcher 3",
    image: "/placeholder.svg?height=300&width=200",
  },
  {
    id: 10,
    title: "Portal 2",
    image: "/placeholder.svg?height=300&width=200",
  },
  {
    id: 11,
    title: "Half-Life: Alyx",
    image: "/placeholder.svg?height=300&width=200",
  },
  {
    id: 12,
    title: "Elden Ring",
    image: "/placeholder.svg?height=300&width=200",
  },
]

export default function GameCatalog() {
  const [cardSize, setCardSize] = useState(50)
  const [view, setView] = useState<"grid" | "list">("grid")

  // Calculate columns based on card size
  const getGridCols = () => {
    if (cardSize <= 30) return 'grid-cols-8'
    if (cardSize <= 40) return 'grid-cols-6'
    if (cardSize <= 50) return 'grid-cols-4'
    if (cardSize <= 60) return 'grid-cols-3'
    return 'grid-cols-2'
  }

  return (
    <div>
      {/*<ControlBar*/}
      {/*  onSizeChange={setCardSize}*/}
      {/*  onViewChange={setView}*/}
      {/*/>*/}

      <div className={`grid ${getGridCols()} gap-4 transition-all duration-300`}>
        {games.map((game) => (
          <Card
            key={game.id}
            className="group relative bg-[#1a1a1f] border-0 overflow-hidden cursor-pointer transition-transform hover:translate-y-[-4px]"
          >
            <div className="relative aspect-[2/3]">
              <Image
                src={game.image}
                alt={game.title}
                fill
                className="object-cover"
              />
              {game.new && (
                <div className="absolute top-2 left-2 bg-teal-500 text-xs px-2 py-0.5 rounded">
                  NEW
                </div>
              )}
              <div className="absolute inset-0 bg-gradient-to-t from-black/60 to-transparent opacity-0 group-hover:opacity-100 transition-opacity">
                <div className="absolute bottom-2 left-2 right-2">
                  <h3 className="text-sm text-white font-medium truncate">
                    {game.title}
                  </h3>
                </div>
              </div>
            </div>
          </Card>
        ))}
      </div>
    </div>
  )
}

