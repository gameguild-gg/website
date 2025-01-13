'use client';

import React, { useEffect, useRef } from 'react';
import mermaid from 'mermaid';

interface MermaidProps {
  chart: string;
}

const Mermaid: React.FC<MermaidProps> = ({ chart }) => {
  const ref = useRef<HTMLDivElement>(null);
  console.log(chart);

  useEffect(() => {
    mermaid.initialize({ startOnLoad: true });

    if (ref.current) {
      mermaid.render('mermaid', chart).then((result) => {
        console.log(result);
        ref.current!.innerHTML = result.svg;
      });
    }
  }, [chart]);

  return <div ref={ref} />;
};

export default Mermaid;
