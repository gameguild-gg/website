'use client';

import React, { useEffect, useRef, useState } from 'react';
import Reveal from 'reveal.js';
import Markdown from 'reveal.js/plugin/markdown/markdown.esm.js';
import Highlight from 'reveal.js/plugin/highlight/highlight.esm.js';
import { Maximize2, Minimize2 } from 'lucide-react';
import 'reveal.js/dist/reveal.css';
import 'reveal.js/dist/theme/white.css';

interface RevealJSProps {
  content: string;
}

const RevealJS: React.FC<RevealJSProps> = ({ content }) => {
  const containerRef = useRef<HTMLDivElement>(null);
  const slidesRef = useRef<HTMLDivElement>(null);
  const revealInstanceRef = useRef<Reveal.Api | null>(null);
  const [isFullscreen, setIsFullscreen] = useState(false);
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    if (
      typeof window === 'undefined' ||
      !containerRef.current ||
      !slidesRef.current
    ) {
      return;
    }

    const initializeReveal = async () => {
      try {
        // Ensure the content is wrapped in `<section>` tags for Reveal.js
        slidesRef.current!.innerHTML = content;

        // Initialize Reveal.js
        const revealInstance = new Reveal(containerRef.current, {
          plugins: [Markdown, Highlight],
          width: '100%',
          height: '100%',
          margin: 0.04,
          embedded: true,
          hash: false,
          mouseWheel: false,
          transition: 'none',
        });

        await revealInstance.initialize();
        revealInstanceRef.current = revealInstance;
      } catch (err) {
        setError(`Error initializing Reveal.js: ${err}`);
      }
    };

    initializeReveal();

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
          slidesRef.current.innerHTML = content;
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
    } else {
      document.exitFullscreen();
      setIsFullscreen(false);
    }
  };

  if (error) {
    return <div className="error-message">Error: {error}</div>;
  }

  return (
    <div
      ref={containerRef}
      className="reveal-container relative"
      style={{ width: '100%', height: '600px', overflow: 'hidden' }}
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
