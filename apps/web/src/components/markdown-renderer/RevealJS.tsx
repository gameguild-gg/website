'use client';

import React, { useEffect, useRef, useState } from 'react';
import dynamic from 'next/dynamic';
import { Maximize2, Minimize2 } from 'lucide-react';
import 'reveal.js/dist/reveal.css';
import 'reveal.js/dist/theme/white.css';
import 'highlight.js/styles/monokai.css';

interface RevealJSProps {
  content: string;
}

const RevealJS: React.FC<RevealJSProps> = ({ content }) => {
  const containerRef = useRef<HTMLDivElement>(null);
  const slidesRef = useRef<HTMLDivElement>(null);
  const revealInstanceRef = useRef<any>(null);
  const [isFullscreen, setIsFullscreen] = useState(false);
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    let Reveal: any;
    let Markdown: any;
    let Highlight: any;

    const loadRevealJS = async () => {
      try {
        // Importação dinâmica
        const revealModule = await import('reveal.js');
        const markdownModule = await import(
          'reveal.js/plugin/markdown/markdown.esm.js'
        );
        const highlightModule = await import(
          'reveal.js/plugin/highlight/highlight.esm.js'
        );

        Reveal = revealModule.default;
        Markdown = markdownModule.default;
        Highlight = highlightModule.default;

        if (!containerRef.current || !slidesRef.current) return;

        slidesRef.current.innerHTML = `<section data-markdown><textarea data-template>${content}</textarea></section>`;

        // Inicializa o Reveal.js
        const revealInstance = new Reveal(containerRef.current, {
          plugins: [Markdown, Highlight],
          width: '100%',
          height: '100%',
          margin: 0.04,
          embedded: true,
          hash: false,
          mouseWheel: true,
          transition: 'none',
          controls: true,
          progress: true,
          controlsTutorial: true,
          controlsLayout: 'bottom-right',
          center: true,
          touch: true,
          minScale: 0.2,
          maxScale: 2.0,
          highlight: {
            highlightOnLoad: true,
            escapeHTML: false,
          },
        });

        await revealInstance.initialize();
        revealInstanceRef.current = revealInstance;
      } catch (err) {
        setError(`Error initializing Reveal.js: ${err}`);
      }
    };

    loadRevealJS();

    return () => {
      if (revealInstanceRef.current) {
        revealInstanceRef.current.destroy();
        revealInstanceRef.current = null;
      }
    };
  }, []);

  useEffect(() => {
    const updateContent = async () => {
      if (revealInstanceRef.current && slidesRef.current) {
        try {
          slidesRef.current.innerHTML = `<section data-markdown><textarea data-template>${content}</textarea></section>`;
          await revealInstanceRef.current.sync();
        } catch (err) {
          setError(`Error syncing Reveal.js: ${err}`);
        }
      }
    };

    updateContent();
  }, [content]);

  const toggleFullscreen = () => {
    if (!document.fullscreenElement) {
      containerRef.current?.requestFullscreen();
      setIsFullscreen(true);
      if (revealInstanceRef.current) {
        revealInstanceRef.current.configure({ embedded: false });
      }
    } else {
      document.exitFullscreen();
      setIsFullscreen(false);
      if (revealInstanceRef.current) {
        revealInstanceRef.current.configure({ embedded: true });
      }
    }
  };

  if (error) {
    return <div className="error-message">Error: {error}</div>;
  }

  return (
    <div
      ref={containerRef}
      className="reveal-container relative"
      style={{
        width: '100%',
        height: '100vh',
        maxHeight: '600px',
        overflow: 'hidden',
      }}
    >
      <div className="reveal">
        <div className="slides" ref={slidesRef}></div>
      </div>
      <button
        onClick={toggleFullscreen}
        className="absolute top-4 right-4 bg-blue-500 hover:bg-blue-600 text-white p-2 rounded-full shadow-lg transition-colors duration-200 z-10"
        aria-label={isFullscreen ? 'Exit fullscreen' : 'Enter fullscreen'}
      >
        {isFullscreen ? <Minimize2 size={24} /> : <Maximize2 size={24} />}
      </button>
    </div>
  );
};

export default RevealJS;
