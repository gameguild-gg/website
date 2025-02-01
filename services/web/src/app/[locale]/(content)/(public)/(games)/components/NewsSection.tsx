import Image from "next/image"
import { Card, CardContent } from "@/components/ui/card"

const newsItems = [
  {
    id: 1,
    title: "Spider-Man 2 Breaks PlayStation Records",
    date: "12 December 2023",
    image: "/placeholder.svg?height=200&width=400",
    views: 486
  },
  {
    id: 2,
    title: "New Expansion Announced for Popular RPG",
    date: "12 December 2023",
    image: "/placeholder.svg?height=200&width=400",
    views: 385
  },
  {
    id: 3,
    title: "Upcoming Indie Game Showcase Event",
    date: "12 December 2023",
    image: "/placeholder.svg?height=200&width=400",
    views: 299
  },
  {
    id: 4,
    title: "Major Update Coming to Battle Royale Hit",
    date: "12 December 2023",
    image: "/placeholder.svg?height=200&width=400",
    views: 458
  },
]

export default function NewsSection() {
  return (
    <div className="mt-12 mb-16">
      <div className="flex justify-between items-center mb-4">
        <h2 className="text-xl font-medium text-white">New and Interesting</h2>
        <a href="#" className="text-[#1abc9c] text-sm hover:text-[#1abc9c]/80">
          View all →
        </a>
      </div>
      <div className="grid grid-cols-1 md:grid-cols-2 lg:grid-cols-4 gap-4">
        {newsItems.map((item) => (
          <Card key={item.id} className="bg-[#1a1a1f] border-0 overflow-hidden">
            <div className="relative aspect-video">
              <Image
                src={item.image}
                alt={item.title}
                fill
                className="object-cover"
              />
            </div>
            <CardContent className="p-4">
              <h3 className="text-white font-medium mb-2 line-clamp-2">
                {item.title}
              </h3>
              <div className="flex justify-between text-sm text-gray-400">
                <span>{item.date}</span>
                <span>👁 {item.views}</span>
              </div>
            </CardContent>
          </Card>
        ))}
      </div>
    </div>
  )
}

