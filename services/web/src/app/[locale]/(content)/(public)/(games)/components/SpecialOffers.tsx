import Image from "next/image"
import { Card } from "@/components/ui/card"

const specialOffers = [
  {
    id: 1,
    title: "Hogwarts Legacy",
    image: "/placeholder.svg?height=300&width=500",
  },
  {
    id: 2,
    title: "Call of Duty: Infinite Warfare",
    image: "/placeholder.svg?height=300&width=500",
  },
  {
    id: 3,
    title: "FIFA 23",
    image: "/placeholder.svg?height=300&width=500",
  },
  {
    id: 4,
    title: "Mass Effect: Andromeda",
    image: "/placeholder.svg?height=300&width=500",
  },
]

export default function SpecialOffers() {
  return (
    <div className="mt-8">
      <div className="flex justify-between items-center mb-4">
        <h2 className="text-xl font-medium text-white">Sponsored Games</h2>
        <button className="text-[#1abc9c] text-sm hover:text-[#1abc9c]/80">
          View all
        </button>
      </div>
      <div className="grid grid-cols-4 gap-4">
        {specialOffers.map((game) => (
          <Card
            key={game.id}
            className="group relative bg-[#1a1a1f] border-0 overflow-hidden rounded-lg cursor-pointer transition-transform hover:translate-y-[-4px]"
          >
            <div className="relative aspect-[5/3]">
              <Image
                src={game.image}
                alt={game.title}
                fill
                className="object-cover"
              />
            </div>
          </Card>
        ))}
      </div>
    </div>
  )
}

