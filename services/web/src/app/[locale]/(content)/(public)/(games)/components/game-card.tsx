import Image from 'next/image'
import { Button } from '@/components/ui/button'
import { Card, CardContent, CardFooter, CardHeader, CardTitle } from '@/components/ui/card'

interface GameCardProps {
  title: string
  genre: string
  imageUrl: string
}

export default function GameCard({ title, genre, imageUrl }: GameCardProps) {
  return (
    <Card className="overflow-hidden">
      <CardHeader className="p-0">
        <Image src={imageUrl} alt={title} width={300} height={200} className="w-full h-48 object-cover" />
      </CardHeader>
      <CardContent className="p-4">
        <CardTitle className="text-xl mb-2">{title}</CardTitle>
        <p className="text-sm text-gray-500">{genre}</p>
      </CardContent>
      <CardFooter className="p-4 pt-0">
        <Button className="w-full">Play Now</Button>
      </CardFooter>
    </Card>
  )
}

