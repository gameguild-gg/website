'use client';

import { useState } from "react"
import Image from "next/image"
import { Card } from "@/components/ui/card"
import { ChevronLeft, ChevronRight } from 'lucide-react'
import { Button } from "@/components/ui/button"

interface GuildPick {
  id: number
  title: string
  image: string
  badge?: string
  description: string
}

interface GuildPicksProps {
  picks: GuildPick[]
}

export default function GuildPicks({ picks }: GuildPicksProps) {
  const [currentSlide, setCurrentSlide] = useState(0)

  const nextSlide = () => {
    setCurrentSlide((prev) => (prev + 1) % picks.length)
  }

  const prevSlide = () => {
    setCurrentSlide((prev) => (prev - 1 + picks.length) % picks.length)
  }

  return (
    <div className="space-y-8">
      <h2 className="text-2xl font-bold text-white">Guild's Picks</h2>
      <div className="relative">
        <div className="relative group">
          <div className="overflow-hidden rounded-lg">
            <div
              className="flex transition-transform duration-500 ease-out w-full"
              style={{ transform: `translateX(-${currentSlide * 100}%)` }}
            >
              {picks.map((game) => (
                <div key={game.id} className="w-full flex-shrink-0">
                  <Card className="relative aspect-[21/9] border-0 overflow-hidden">
                    <div className="absolute inset-0">
                      <Image
                        src={game.image}
                        alt={game.title}
                        layout="fill"
                        objectFit="cover"
                      />
                    </div>
                    <div className="absolute inset-0 bg-gradient-to-t from-black/60 to-transparent">
                      <div className="absolute bottom-6 left-6 right-6">
                        <h3 className="text-3xl font-bold text-white mb-2">{game.title}</h3>
                        <p className="text-lg text-white/80">{game.description}</p>
                      </div>
                    </div>
                    {game.badge && (
                      <div className="absolute top-4 left-4 bg-yellow-500/90 px-2 py-1 rounded text-sm font-medium">
                        {game.badge}
                      </div>
                    )}
                  </Card>
                </div>
              ))}
            </div>
          </div>

          <Button
            variant="ghost"
            size="icon"
            className="absolute left-4 top-1/2 -translate-y-1/2 bg-black/50 text-white rounded-full opacity-0 group-hover:opacity-100 transition-opacity"
            onClick={prevSlide}
          >
            <ChevronLeft className="h-6 w-6" />
          </Button>

          <Button
            variant="ghost"
            size="icon"
            className="absolute right-4 top-1/2 -translate-y-1/2 bg-black/50 text-white rounded-full opacity-0 group-hover:opacity-100 transition-opacity"
            onClick={nextSlide}
          >
            <ChevronRight className="h-6 w-6" />
          </Button>

          <div className="absolute bottom-4 left-1/2 -translate-x-1/2 flex gap-2">
            {picks.map((_, index) => (
              <button
                key={index}
                className={`w-2 h-2 rounded-full transition-colors ${
                  currentSlide === index ? 'bg-white' : 'bg-white/30'
                }`}
                onClick={() => setCurrentSlide(index)}
              />
            ))}
          </div>
        </div>
      </div>
    </div>
  )
}

