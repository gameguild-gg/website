'use client';

import React, { useEffect, useRef, useState } from 'react';
import mermaid from 'mermaid';

interface MermaidProps {
  chart: string;
}

const Mermaid: React.FC<MermaidProps> = ({ chart }) => {
  const ref = useRef<HTMLDivElement>(null);
  console.log(chart);

  const [svg, setSvg] = useState<string | null>(null);

  useEffect(() => {
    mermaid.initialize({ startOnLoad: true });

    if (ref.current) {
      mermaid.render('mermaid', chart).then((result) => {
        console.log(result);
        ref.current!.innerHTML = result.svg;
        setSvg(result.svg);
      });
    }
  }, [chart, svg]);

  return <div ref={ref} />;
};

export default Mermaid;
