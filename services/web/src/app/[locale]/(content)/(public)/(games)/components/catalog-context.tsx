"use client"

import React, { createContext, useContext, useState, ReactNode } from 'react'

type Genre = 'trending' | 'action' | 'shooter' | 'strategy' | 'adventure' | 'rpg' | 'sports'
type FilterContext = 'All Games' | 'My Library' | 'Wishlist' | 'Recently Played' | 'New Releases'
type ViewType = 'grid' | 'list'
type Platform = 'all' | 'pc' | 'playstation' | 'xbox'
type SortBy = 'last-played' | 'name' | 'size'

interface CatalogContextType {
  activeGenre: Genre
  setActiveGenre: (genre: Genre) => void
  filterContext: FilterContext
  setFilterContext: (context: FilterContext) => void
  view: ViewType
  setView: (view: ViewType) => void
  cardSize: number
  setCardSize: (size: number) => void
  platform: Platform
  setPlatform: (platform: Platform) => void
  sortBy: SortBy
  setSortBy: (sortBy: SortBy) => void
}

const CatalogContext = createContext<CatalogContextType | undefined>(undefined)

export function CatalogProvider({ children }: { children: ReactNode }) {
  const [activeGenre, setActiveGenre] = useState<Genre>('trending')
  const [filterContext, setFilterContext] = useState<FilterContext>('All Games')
  const [view, setView] = useState<ViewType>('grid')
  const [cardSize, setCardSize] = useState(50)
  const [platform, setPlatform] = useState<Platform>('all')
  const [sortBy, setSortBy] = useState<SortBy>('last-played')

  return (
    <CatalogContext.Provider value={{
      activeGenre, setActiveGenre,
      filterContext, setFilterContext,
      view, setView,
      cardSize, setCardSize,
      platform, setPlatform,
      sortBy, setSortBy
    }}>
      {children}
    </CatalogContext.Provider>
  )
}

export function useCatalog() {
  const context = useContext(CatalogContext)
  if (context === undefined) {
    throw new Error('useCatalog must be used within a CatalogProvider')
  }
  return context
}

