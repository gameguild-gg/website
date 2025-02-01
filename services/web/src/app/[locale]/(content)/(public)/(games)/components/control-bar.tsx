"use client"

import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue
} from "@/components/ui/select"
import { Slider } from "@/components/ui/slider"
import { LayoutGrid, List, Flame, Sword, Crosshair, Brain, Rocket, Gamepad, Trophy, Filter, ChevronLeft, ChevronRight, PuzzleIcon as PuzzlePiece, Car, Cog, Ghost, Layers, Globe, Tent, Swords } from 'lucide-react'
import { Button } from "@/components/ui/button"
import {
  DropdownMenu,
  DropdownMenuContent,
  DropdownMenuLabel,
  DropdownMenuRadioGroup,
  DropdownMenuRadioItem,
  DropdownMenuSeparator,
  DropdownMenuTrigger,
} from "@/components/ui/dropdown-menu"

import { useRef, useState, useEffect } from 'react';
import { cn } from "@/lib/utils"
import { useCatalog } from '@/app/[locale]/(content)/(public)/(games)/components/catalog-context';

const genres = [
  { id: 'trending', label: 'Trending', icon: Flame },
  { id: 'action', label: 'Action', icon: Sword },
  { id: 'shooter', label: 'Shooter', icon: Crosshair },
  { id: 'strategy', label: 'Strategy', icon: Brain },
  { id: 'adventure', label: 'Adventure', icon: Rocket },
  { id: 'rpg', label: 'RPG', icon: Gamepad },
  { id: 'sports', label: 'Sports', icon: Trophy },
  { id: 'puzzle', label: 'Puzzle', icon: PuzzlePiece },
  { id: 'racing', label: 'Racing', icon: Car },
  { id: 'simulation', label: 'Simulation', icon: Cog },
  { id: 'horror', label: 'Horror', icon: Ghost },
  { id: 'platformer', label: 'Platformer', icon: Layers },
  { id: 'openworld', label: 'Open World', icon: Globe },
  { id: 'survival', label: 'Survival', icon: Tent },
  { id: 'fighting', label: 'Fighting', icon: Swords }
]

const filterContexts = [
  "All Games",
  "My Library",
  "Wishlist",
  "Recently Played",
  "New Releases",
]

export default function ControlBar() {
  const {
    activeGenre, setActiveGenre,
    filterContext, setFilterContext,
    view, setView,
    cardSize, setCardSize,
    platform, setPlatform,
    sortBy, setSortBy
  } = useCatalog()

  const genreContainerRef = useRef<HTMLDivElement>(null)
  const [showLeftScroll, setShowLeftScroll] = useState(false)
  const [showRightScroll, setShowRightScroll] = useState(false)

  useEffect(() => {
    const container = genreContainerRef.current
    if (container) {
      const checkForOverflow = () => {
        setShowLeftScroll(container.scrollLeft > 0)
        setShowRightScroll(container.scrollLeft < container.scrollWidth - container.clientWidth)
      }
      checkForOverflow()
      container.addEventListener('scroll', checkForOverflow)
      window.addEventListener('resize', checkForOverflow)
      return () => {
        container.removeEventListener('scroll', checkForOverflow)
        window.removeEventListener('resize', checkForOverflow)
      }
    }
  }, [])

  const scrollLeft = () => {
    if (genreContainerRef.current) {
      genreContainerRef.current.scrollBy({ left: -300, behavior: 'smooth' })
    }
  }

  const scrollRight = () => {
    if (genreContainerRef.current) {
      genreContainerRef.current.scrollBy({ left: 300, behavior: 'smooth' })
    }
  }


  return (
    <div className="space-y-4 overflow-hidden">
      <div className="flex items-center justify-between pb-4 border-b border-gray-800">
        <div className="flex items-center flex-1 overflow-hidden">
          {showLeftScroll && (
            <Button variant="ghost" size="icon" onClick={scrollLeft} className="mr-2 flex-shrink-0">
              <ChevronLeft className="h-4 w-4" />
            </Button>
          )}
          <div ref={genreContainerRef} className="flex gap-2 overflow-x-scroll scrollbar-hide [&::-webkit-scrollbar]:hidden [-ms-overflow-style:'none'] [scrollbar-width:'none']">
            {genres.map((genre) => {
              const Icon = genre.icon
              return (
                <Button
                  key={genre.id}
                  variant="ghost"
                  className={`flex items-center gap-2 px-4 py-2 rounded-lg flex-shrink-0 ${
                    activeGenre === genre.id 
                      ? 'bg-[#2a2a2f] text-white' 
                      : 'text-gray-400 hover:text-white hover:bg-[#2a2a2f]'
                  }`}
                  onClick={() => setActiveGenre(genre.id as any)}
                >
                  <Icon className="h-4 w-4" />
                  <span className="hidden sm:inline">{genre.label}</span>
                </Button>
              )
            })}
          </div>
          {showRightScroll && (
            <Button variant="ghost" size="icon" onClick={scrollRight} className="ml-2 flex-shrink-0">
              <ChevronRight className="h-4 w-4" />
            </Button>
          )}
        </div>
        <DropdownMenu>
          <DropdownMenuTrigger asChild>
            <Button variant="outline" className="ml-4 flex-shrink-0">
              <Filter className="mr-2 h-4 w-4" />
              <span className="hidden sm:inline">{filterContext}</span>
            </Button>
          </DropdownMenuTrigger>
          <DropdownMenuContent className="w-56">
            <DropdownMenuLabel>Filter Context</DropdownMenuLabel>
            <DropdownMenuSeparator />
            <DropdownMenuRadioGroup value={filterContext} onValueChange={(value) => setFilterContext(value as any)}>
              {filterContexts.map((context) => (
                <DropdownMenuRadioItem key={context} value={context}>
                  {context}
                </DropdownMenuRadioItem>
              ))}
            </DropdownMenuRadioGroup>
          </DropdownMenuContent>
        </DropdownMenu>
      </div>

      <div className="flex flex-wrap items-center justify-between gap-4 bg-[#1a1a1f] p-3 rounded-lg">
        <div className="flex flex-wrap items-center gap-4">
          <Select value={platform} onValueChange={(value) => setPlatform(value as any)}>
            <SelectTrigger className="w-[180px] bg-transparent border-0 text-gray-300 hover:text-white">
              <SelectValue placeholder="Show: All Platforms" />
            </SelectTrigger>
            <SelectContent>
              <SelectItem value="all">All Platforms</SelectItem>
              <SelectItem value="pc">PC</SelectItem>
              <SelectItem value="playstation">PlayStation</SelectItem>
              <SelectItem value="xbox">Xbox</SelectItem>
            </SelectContent>
          </Select>

          <Select value={sortBy} onValueChange={(value) => setSortBy(value as any)}>
            <SelectTrigger className="w-[180px] bg-transparent border-0 text-gray-300 hover:text-white">
              <SelectValue placeholder="Sort by: Last played" />
            </SelectTrigger>
            <SelectContent>
              <SelectItem value="last-played">Last played</SelectItem>
              <SelectItem value="name">Name</SelectItem>
              <SelectItem value="size">Size</SelectItem>
            </SelectContent>
          </Select>
        </div>

        <div className="flex items-center gap-6">
          <div className="flex items-center gap-2">
            <span className="text-sm text-gray-400">Size</span>
            <Slider
              value={[cardSize]}
              max={100}
              min={30}
              step={10}
              className="w-32"
              onValueChange={([value]) => setCardSize(value)}
            />
          </div>

          <div className="flex gap-1">
            <Button
              variant="ghost"
              size="icon"
              className={`${view === 'grid' ? 'bg-[#2a2a2f]' : ''} text-gray-400 hover:text-white`}
              onClick={() => setView('grid')}
            >
              <LayoutGrid className="h-4 w-4" />
            </Button>
            <Button
              variant="ghost"
              size="icon"
              className={`${view === 'list' ? 'bg-[#2a2a2f]' : ''} text-gray-400 hover:text-white`}
              onClick={() => setView('list')}
            >
              <List className="h-4 w-4" />
            </Button>
          </div>
        </div>
      </div>
    </div>
  )
}

