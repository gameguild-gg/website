import React, { useState, useEffect } from "react";

interface CarouselProps {
  images: string[];
}

const Carousel: React.FC<CarouselProps> = ({ images }) => {
  const [currentIndex, setCurrentIndex] = useState<number>(0);
  const [isHovered, setIsHovered] = useState<boolean>(false);
  const [fadeEffect, setFadeEffect] = useState<boolean>(false);

  // Função para ir ao próximo slide
  const nextSlide = (): void => {
    setFadeEffect(true); // Inicia o efeito de transição
    setTimeout(() => {
      setCurrentIndex((prevIndex) =>
        prevIndex === images.length - 1 ? 0 : prevIndex + 1
      );
      setFadeEffect(false); // Remove o efeito após a transição
    }, 500); // Tempo do efeito (em ms)
  };

  // Troca para um slide específico
  const goToSlide = (index: number): void => {
    setFadeEffect(true); // Inicia o efeito de transição
    setTimeout(() => {
      setCurrentIndex(index);
      setFadeEffect(false); // Remove o efeito após a transição
    }, 500); // Tempo do efeito (em ms)
  };

  // Rolagem automática
  useEffect(() => {
    if (!isHovered) {
      const interval = setInterval(nextSlide, 3000); // Troca a cada 3 segundos
      return () => clearInterval(interval); // Limpa o intervalo ao desmontar
    }
  }, [isHovered]);

  return (
    <div
      className="carousel-container relative w-full max-w-4xl mx-auto overflow-hidden"
      onMouseEnter={() => setIsHovered(true)}
      onMouseLeave={() => setIsHovered(false)}
    >
      {/* Imagem Atual */}
      <div
        className={`carousel-slide relative flex justify-center items-center transition-opacity duration-500 ${
          fadeEffect ? "opacity-0" : "opacity-100"
        }`}
      >
        <img
          src={images[currentIndex]}
          alt={`Slide ${currentIndex + 1}`}
          className="w-full max-h-[600px] object-contain"
        />
      </div>

      {/* Seletores */}
      <div className="carousel-controls flex justify-center mt-4 space-x-2">
        {images.map((_, index) => (
          <button
            key={index}
            onClick={() => goToSlide(index)}
            className={`carousel-dot w-4 h-4 rounded-full ${
              index === currentIndex ? "bg-blue-500" : "bg-gray-400"
            }`}
          />
        ))}
      </div>
    </div>
  );
};

export { Carousel };
