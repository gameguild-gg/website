import Image from "next/image"
import { Card, CardContent } from "@/components/ui/card"
import { Button } from "@/components/ui/button"

const featuredGames = [
  {
    id: 1,
    title: "Destiny 2: Lightfall",
    description: "Embark on a journey through the solar system and experience the next chapter in the Destiny 2 saga.",
    image: "/placeholder.svg?height=400&width=800",
    price: 49.99,
  },
  // Add more featured games
]

export default function FeaturedGames() {
  return (
    <div className="space-y-4">
      <h2 className="text-xl font-bold">Featured Games</h2>
      <div className="grid grid-cols-1 gap-6">
        {featuredGames.map((game) => (
          <Card key={game.id} className="bg-[#1a1a1f] border-gray-800 overflow-hidden">
            <div className="relative aspect-[21/9]">
              <Image
                src={game.image}
                alt={game.title}
                fill
                className="object-cover"
              />
            </div>
            <CardContent className="p-6">
              <div className="flex items-start justify-between">
                <div>
                  <h3 className="text-2xl font-bold">{game.title}</h3>
                  <p className="text-gray-400 mt-2 max-w-2xl">{game.description}</p>
                </div>
                <Button className="bg-blue-600 hover:bg-blue-700">
                  ${game.price} - Buy Now
                </Button>
              </div>
            </CardContent>
          </Card>
        ))}
      </div>
    </div>
  )
}

