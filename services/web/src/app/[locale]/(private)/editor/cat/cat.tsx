'use client';

import React, { useState } from 'react';

interface HexagonProps {
  isActive: boolean;
  onClick: () => void;
}

const Hexagon: React.FC<HexagonProps> = ({ isActive, onClick }) => (
  <div
    className={`w-14 h-14 m-[1px] relative cursor-pointer transition-colors duration-300 ${
      isActive ? 'bg-green-800' : 'bg-lime-300'
    }`}
    onClick={onClick}
    style={{
      clipPath: 'polygon(50% 0%, 100% 25%, 100% 75%, 50% 100%, 0% 75%, 0% 25%)',
    }}
  />
);

const CatSilhouette: React.FC = () => (
  <div className="absolute inset-0 flex items-center justify-center pointer-events-none">
    <svg
      xmlns="http://www.w3.org/2000/svg"
      viewBox="0 0 24 24"
      className="w-16 h-16 fill-current text-black"
    >
      <path d="M12,8L10.67,8.09C9.81,7.07 7.4,4.5 5,4.5C5,4.5 3.03,7.46 4.96,11.41C4.41,12.24 4.07,12.67 4,13.66L2.07,13.95L2.28,14.93L4.04,14.67L4.18,15.38L2.61,16.32L3.08,17.21L4.53,16.32C5.68,18.76 8.59,20 12,20C15.41,20 18.32,18.76 19.47,16.32L20.92,17.21L21.39,16.32L19.82,15.38L19.96,14.67L21.72,14.93L21.93,13.95L20,13.66C19.93,12.67 19.59,12.24 19.04,11.41C20.97,7.46 19,4.5 19,4.5C16.6,4.5 14.19,7.07 13.33,8.09L12,8M9,11A1,1 0 0,1 10,12A1,1 0 0,1 9,13A1,1 0 0,1 8,12A1,1 0 0,1 9,11M15,11A1,1 0 0,1 16,12A1,1 0 0,1 15,13A1,1 0 0,1 14,12A1,1 0 0,1 15,11M11,14H13L12,15.5L11,14Z" />
    </svg>
  </div>
);

export default function HexagonalGrid() {
  const rows = 13;
  const cols = 13;
  const [activeHexagons, setActiveHexagons] = useState<boolean[][]>(
    Array(rows)
      .fill(null)
      .map(() => Array(cols).fill(false)),
  );

  const handleHexagonClick = (row: number, col: number) => {
    const newActiveHexagons = activeHexagons.map((r, i) =>
      r.map((h, j) => (i === row && j === col ? !h : h)),
    );
    setActiveHexagons(newActiveHexagons);
  };

  return (
    <div className="flex flex-col items-center justify-center min-h-screen bg-gray-100">
      <div className="relative">
        {activeHexagons.map((row, rowIndex) => (
          <div
            key={rowIndex}
            className="flex justify-center"
            style={{
              marginLeft: rowIndex % 2 ? '-56px' : '0px',
              marginTop: rowIndex !== 0 ? '-0.8rem' : '0',
            }}
          >
            {row.map((isActive, colIndex) => (
              <Hexagon
                key={`${rowIndex}-${colIndex}`}
                isActive={isActive}
                onClick={() => handleHexagonClick(rowIndex, colIndex)}
              />
            ))}
          </div>
        ))}
        <CatSilhouette />
      </div>
    </div>
  );
}
