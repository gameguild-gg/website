import Image from "next/image"
import { DiscIcon as Discord, Youtube } from 'lucide-react'

export default function Footer() {
  return (
    <footer className="bg-[#0f1115] pt-16 pb-8 border-t border-gray-800">
      <div className="container mx-auto px-4">
        <div className="mb-12">
          <h2 className="text-xl font-medium text-white mb-4">
            Game Testing Lab — A Platform for Game Testing and Feedback
          </h2>
          <p className="text-gray-400 max-w-3xl">
            Game Testing Lab optimizes Fortnite, Cyberpunk 2077, PUBG, CS:GO, GTA 5, Minecraft, Genshin Impact, Borderlands 3, World of Tanks, Rust, Call of Duty: Warzone, Destiny 2, War Thunder and Valorant. Game Testing Lab helps you test and provide feedback for games.
          </p>
        </div>

        <div className="grid grid-cols-2 md:grid-cols-4 gap-8 mb-12">
          <div>
            <h3 className="text-white font-medium mb-4">Platform</h3>
            <ul className="space-y-2">
              <li><a href="#" className="text-gray-400 hover:text-white">About</a></li>
              <li><a href="#" className="text-gray-400 hover:text-white">Games</a></li>
              <li><a href="#" className="text-gray-400 hover:text-white">Testing</a></li>
              <li><a href="#" className="text-gray-400 hover:text-white">System</a></li>
            </ul>
          </div>
          <div>
            <h3 className="text-white font-medium mb-4">Support</h3>
            <ul className="space-y-2">
              <li><a href="#" className="text-gray-400 hover:text-white">Contact</a></li>
              <li><a href="#" className="text-gray-400 hover:text-white">Help Center</a></li>
              <li><a href="#" className="text-gray-400 hover:text-white">Privacy Policy</a></li>
              <li><a href="#" className="text-gray-400 hover:text-white">Terms</a></li>
            </ul>
          </div>
          <div>
            <h3 className="text-white font-medium mb-4">Follow Us</h3>
            <div className="flex space-x-4">
              <a href="#" className="text-gray-400 hover:text-white">
                <Discord className="w-6 h-6" />
              </a>
              <a href="#" className="text-gray-400 hover:text-white">
                <Youtube className="w-6 h-6" />
              </a>
            </div>
          </div>
        </div>

        <div className="flex items-center justify-between pt-8 border-t border-gray-800">
          <div className="flex items-center space-x-2">
            <div className="w-8 h-8 bg-[#1abc9c] rounded flex items-center justify-center">
              <span className="text-white font-bold">GL</span>
            </div>
            <span className="text-gray-400">© 2023 Game Testing Lab</span>
          </div>
          <div className="flex space-x-6">
            <a href="#" className="text-gray-400 hover:text-white text-sm">Privacy</a>
            <a href="#" className="text-gray-400 hover:text-white text-sm">Terms</a>
            <a href="#" className="text-gray-400 hover:text-white text-sm">Cookies</a>
          </div>
        </div>
      </div>
    </footer>
  )
}

