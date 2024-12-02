'use client'

import { useState, useEffect } from 'react'
import Image from 'next/image'
import { Carousel, CarouselContent, CarouselItem, CarouselNext, CarouselPrevious } from "@/components/courses/carousel"
import { Card, CardContent } from "@/components/courses/card"

type ImageCarouselProps = {
  images: string[]
}

export function ImageCarousel({ images }: ImageCarouselProps) {
  const [currentIndex, setCurrentIndex] = useState(0)

  useEffect(() => {
    const timer = setInterval(() => {
      setCurrentIndex((prevIndex) => (prevIndex + 1) % images.length)
    }, 4000)

    return () => clearInterval(timer)
  }, [images.length])

  return (
    <div className="relative">
      <Carousel className="w-full max-w-xl mx-auto">
        <CarouselContent>
          {images.map((src, index) => (
            <CarouselItem key={index} className="pl-0">
              <Card>
                <CardContent className="flex aspect-square items-center justify-center p-0">
                  <Image
                    src={src}
                    alt={`Course Image ${index + 1}`}
                    width={600}
                    height={400}
                    className="rounded-lg object-cover w-full h-full"
                  />
                </CardContent>
              </Card>
            </CarouselItem>
          ))}
        </CarouselContent>
        <CarouselPrevious />
        <CarouselNext />
      </Carousel>
      <div className="flex justify-center mt-4">
        {images.map((_, index) => (
          <button
            key={index}
            className={`w-3 h-3 rounded-full mx-1 ${
              index === currentIndex ? 'bg-primary' : 'bg-gray-300'
            }`}
            onClick={() => setCurrentIndex(index)}
          />
        ))}
      </div>
    </div>
  )
}

