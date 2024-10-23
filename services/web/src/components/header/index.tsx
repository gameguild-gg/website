'use client'

import { useState } from 'react'
import Link from 'next/link'
import { Button } from "@/components/ui/button"
import { Input } from "@/components/ui/input"
import {
  DropdownMenu,
  DropdownMenuContent,
  DropdownMenuItem,
  DropdownMenuTrigger,
} from "@/components/ui/dropdown-menu"
import { ChevronUp, ChevronDown, Search, X, Globe, Bell, Menu } from 'lucide-react'
import { useRouter } from 'next/navigation'

export default function Header({ user = null }: { user?: { name: string; avatar: string } | null }) {
  const [isSearchOpen, setIsSearchOpen] = useState(false)
  const [isMobileMenuOpen, setIsMobileMenuOpen] = useState(false)
  const [isMoreOpen, setIsMoreOpen] = useState(false)

  const menuItems = ['Blog', 'Games', 'Tests', 'Jams', 'Jobs']
  const moreItems = ['Item 1', 'Item 2', 'Item 3']
  const languages = ['English', 'Spanish', 'French', 'German']

  const router = useRouter();

  return (
    <header className="bg-neutral-900 h-[70px] flex items-center justify-between px-4 text-white">
      <div className="flex items-center">
        <Link href="/" className="mr-3">
          <img
            src="/assets/images/logo-text.png"
            className="w-[135px] h-46px my-auto mx-[10px]"
          />
        </Link>
        <nav className="hidden md:flex space-x-4">
          {menuItems.map((item) => (
            <Link key={item} href={`/${item.toLowerCase()}`} className="ml-6 hover:text-gray-400 transition-colors">
              {item}
            </Link>
          ))}
          <DropdownMenu open={isMoreOpen} onOpenChange={setIsMoreOpen}>
            <DropdownMenuTrigger className="flex items-center hover:text-gray-400 transition-colors">
              More {isMoreOpen ? <ChevronUp className="ml-1 h-4 w-4" /> : <ChevronDown className="ml-1 h-4 w-4" />}
            </DropdownMenuTrigger>
            <DropdownMenuContent>
              {moreItems.map((item) => (
                <DropdownMenuItem key={item}>{item}</DropdownMenuItem>
              ))}
            </DropdownMenuContent>
          </DropdownMenu>
        </nav>
      </div>
      <div className="hidden md:flex items-center space-x-4">
        {isSearchOpen ? (
          <div className="relative">
            <Input
              type="text"
              placeholder="Search..."
              className="pl-10 pr-10 py-2 rounded-full bg-neutral-800 text-white"
            />
            <Search className="absolute left-3 top-1/2 transform -translate-y-1/2 text-gray-400" />
            <button
              onClick={() => setIsSearchOpen(false)}
              className="absolute right-3 top-1/2 transform -translate-y-1/2 text-gray-400 hover:text-white"
            >
              <X className="h-4 w-4" />
            </button>
          </div>
        ) : (
          <button onClick={() => setIsSearchOpen(true)} className="hover:text-gray-400 transition-colors">
            <Search />
          </button>
        )}
        <DropdownMenu>
          <DropdownMenuTrigger className="hover:text-gray-400 transition-colors">
            <Globe />
          </DropdownMenuTrigger>
          <DropdownMenuContent>
            {languages.map((lang) => (
              <DropdownMenuItem key={lang}>{lang}</DropdownMenuItem>
            ))}
          </DropdownMenuContent>
        </DropdownMenu>
        {user ? (
          <div className="flex items-center space-x-2">
            <DropdownMenu>
              <DropdownMenuTrigger className="hover:text-gray-400 transition-colors">
                <Bell />
              </DropdownMenuTrigger>
              <DropdownMenuContent>
                <DropdownMenuItem>Notification 1</DropdownMenuItem>
                <DropdownMenuItem>Notification 2</DropdownMenuItem>
              </DropdownMenuContent>
            </DropdownMenu>
            <DropdownMenu>
              <DropdownMenuTrigger>
                <img src={user.avatar} alt={user.name} className="w-8 h-8 rounded-full" />
              </DropdownMenuTrigger>
              <DropdownMenuContent>
                <DropdownMenuItem>Profile</DropdownMenuItem>
                <DropdownMenuItem>Settings</DropdownMenuItem>
                <DropdownMenuItem>Logout</DropdownMenuItem>
              </DropdownMenuContent>
            </DropdownMenu>
          </div>
        ) : (
          <Button variant="outline" className="bg-white text-black rounded-md hover:bg-gray-200">
            Connect
          </Button>
        )}
      </div>
      <div className="md:hidden">
        <button onClick={() => setIsMobileMenuOpen(!isMobileMenuOpen)} className="text-white">
          {isMobileMenuOpen ? <X /> : <Menu />}
        </button>
      </div>
      {isMobileMenuOpen && (
        <div className="absolute top-[70px] left-0 right-0 bg-neutral-950 p-4 md:hidden z-30">
          <div className="flex flex-col space-y-4">
            <Input
              type="text"
              placeholder="Search..."
              className="pl-10 pr-10 py-2 rounded-full bg-neutral-800 text-white"
            />
            <DropdownMenu>
              <DropdownMenuTrigger className="flex items-center justify-between w-full">
                <span>Language</span>
                <Globe />
              </DropdownMenuTrigger>
              <DropdownMenuContent>
                {languages.map((lang) => (
                  <DropdownMenuItem key={lang}>{lang}</DropdownMenuItem>
                ))}
              </DropdownMenuContent>
            </DropdownMenu>
            {user ? (
              <>
                <DropdownMenu>
                  <DropdownMenuTrigger className="flex items-center justify-between w-full">
                    <span>Notifications</span>
                    <Bell />
                  </DropdownMenuTrigger>
                  <DropdownMenuContent>
                    <DropdownMenuItem>Notification 1</DropdownMenuItem>
                    <DropdownMenuItem>Notification 2</DropdownMenuItem>
                  </DropdownMenuContent>
                </DropdownMenu>
                <DropdownMenu>
                  <DropdownMenuTrigger className="flex items-center space-x-2">
                    <img src={user.avatar} alt={user.name} className="w-8 h-8 rounded-full" />
                    <span>{user.name}</span>
                  </DropdownMenuTrigger>
                  <DropdownMenuContent>
                    <DropdownMenuItem>Profile</DropdownMenuItem>
                    <DropdownMenuItem>Settings</DropdownMenuItem>
                    <DropdownMenuItem>Logout</DropdownMenuItem>
                  </DropdownMenuContent>
                </DropdownMenu>
              </>
            ) : (
              <Button onClick={()=>router.push('/connect')} variant="outline" className="bg-white text-black font-semibold rounded-md hover:bg-gray-200 w-full">
                Connect
              </Button>
            )}
            {menuItems.map((item) => (
              <Link key={item} href={`/${item.toLowerCase()}`} className="hover:text-gray-400 transition-colors">
                {item}
              </Link>
            ))}
            <DropdownMenu>
              <DropdownMenuTrigger className="flex items-center justify-between w-full">
                <span>More</span>
                <ChevronDown className="h-4 w-4" />
              </DropdownMenuTrigger>
              <DropdownMenuContent>
                {moreItems.map((item) => (
                  <DropdownMenuItem key={item}>{item}</DropdownMenuItem>
                ))}
              </DropdownMenuContent>
            </DropdownMenu>
          </div>
        </div>
      )}
    </header>
  )
}