'use client';

import React, { useEffect, useRef, useState } from 'react';
import mermaid from 'mermaid';

interface MermaidProps {
  chart: string;
}

const Mermaid: React.FC<MermaidProps> = ({ chart }) => {
  const containerRef = useRef<HTMLDivElement>(null);
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    mermaid.initialize({
      startOnLoad: false,
      theme: 'dark',
      securityLevel: 'strict',
    });

    const renderChart = async () => {
      if (!containerRef.current) return;

      try {
        containerRef.current.innerHTML = '';
        const { svg } = await mermaid.render(
          `mermaid-${Math.random().toString(36).substr(2, 9)}`,
          chart,
        );
        containerRef.current.innerHTML = svg;
        setError(null);
      } catch (err) {
        console.error('Mermaid rendering failed:', err);
        setError('Failed to render the diagram. Please check your syntax.');
      }
    };

    renderChart();
  }, [chart]);

  if (error) {
    return <div className="text-red-500">{error}</div>;
  }

  return <div ref={containerRef} className="mermaid-container" />;
};

export default Mermaid;
