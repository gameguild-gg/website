'use client';

import type React from 'react';
import { useEffect, useRef, useState } from 'react';
import Reveal from 'reveal.js';
import Markdown from 'reveal.js/plugin/markdown/markdown.esm.js';
import Highlight from 'reveal.js/plugin/highlight/highlight.esm.js';
import 'reveal.js/dist/reveal.css';
import 'reveal.js/dist/theme/white.css';

interface RevealJSProps {
  content: string;
}

const RevealJS: React.FC<RevealJSProps> = ({ content }) => {
  const revealRef = useRef<HTMLDivElement>(null);
  const deckRef = useRef<Reveal.Api | null>(null);
  const [error, setError] = useState<string | null>(null);
  const [isInitialized, setIsInitialized] = useState(false);
  const [isSyncing, setIsSyncing] = useState(false);

  useEffect(() => {
    if (revealRef.current && !deckRef.current) {
      const initReveal = async () => {
        try {
          deckRef.current = new Reveal(revealRef.current, {
            plugins: [Markdown, Highlight],
            width: '100%',
            height: '100%',
            margin: 0.04,
            embedded: true,
            hash: false,
            mouseWheel: false,
            transition: 'none',
          });
          await deckRef.current.initialize();
          setIsInitialized(true);
        } catch (error) {
          setError(`Error initializing Reveal: ${error}`);
        }
      };

      initReveal();
    }

    return () => {
      if (deckRef.current) {
        try {
          deckRef.current.destroy();
          deckRef.current = null;
          setIsInitialized(false);
        } catch (error) {
          console.error('Error destroying Reveal:', error);
        }
      }
    };
  }, []);

  useEffect(() => {
    const updateContent = async () => {
      if (revealRef.current && deckRef.current && isInitialized && !isSyncing) {
        const slidesElement = revealRef.current.querySelector('.slides');
        if (slidesElement) {
          setIsSyncing(true);
          slidesElement.innerHTML = content;
          try {
            await deckRef.current.sync();
            deckRef.current.slide(0, 0, 0);
          } catch (error) {
            setError(`Error syncing Reveal: ${JSON.stringify(error)}`);
          } finally {
            setIsSyncing(false);
          }
        }
      }
    };

    updateContent();
  }, [content, isInitialized, isSyncing]);

  if (error) {
    return <div className="error-message">Error: {error}</div>;
  }

  return (
    <div
      className="reveal-container"
      style={{ width: '100%', height: '600px', overflow: 'hidden' }}
    >
      <div className="reveal" ref={revealRef}>
        <div className="slides">{content}</div>
      </div>
    </div>
  );
};

export default RevealJS;
