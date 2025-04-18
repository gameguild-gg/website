import React, { useEffect, useRef } from 'react';
import { Terminal as Xterm } from 'xterm';
import { FitAddon } from 'xterm-addon-fit';
import 'xterm/css/xterm.css';
import { useMessagePort } from '../module/runner';

function debounce<T extends (...args: any[]) => void>(fn: T, delay = 60): (...args: Parameters<T>) => void {
  let timer: NodeJS.Timeout | null = null;

  return function (this: any, ...args: Parameters<T>) {
    if (timer) clearTimeout(timer);
    timer = setTimeout(() => {
      fn.apply(this, args);
    }, delay);
  };
}

export default function Terminal() {
  const containerRef = useRef<HTMLDivElement | null>(null);
  const messagePort = useMessagePort();
  const xtermRef = useRef<Xterm | null>(null);
  const fitAddonRef = useRef<FitAddon | null>(null);

  // Initialize terminal once on component mount
  useEffect(() => {
    // Create terminal instance
    const xterm = new Xterm({
      theme: {
        background: '#000000',
        foreground: '#ffffff',
      },
      cursorBlink: true,
      fontSize: 14,
    });

    const fitAddon = new FitAddon();
    xterm.loadAddon(fitAddon);

    // Save references
    xtermRef.current = xterm;
    fitAddonRef.current = fitAddon;

    // Mount terminal to DOM
    if (containerRef.current) {
      xterm.open(containerRef.current);
      fitAddon.fit();
    }

    // Setup resize handling
    const handleResize = debounce(() => {
      if (fitAddonRef.current) {
        fitAddonRef.current.fit();
      }
    }, 100);

    const resizeObserver = new ResizeObserver(handleResize);
    if (containerRef.current) {
      resizeObserver.observe(containerRef.current);
    }

    // Cleanup function
    return () => {
      resizeObserver.disconnect();
      if (xtermRef.current) {
        xtermRef.current.dispose();
        xtermRef.current = null;
      }
    };
  }, []);

  // Handle message port updates
  useEffect(() => {
    if (!messagePort || !xtermRef.current) return;

    // Clear terminal when messagePort changes
    xtermRef.current.clear();

    // Handle incoming messages
    const onMessage = (event: MessageEvent) => {
      if (xtermRef.current && event.data && typeof event.data === 'object') {
        switch (event.data.id) {
          case 'write':
            xtermRef.current.writeln(event.data.data);
            break;
        }
      }
    };

    messagePort.onmessage = onMessage;

    return () => {
      if (messagePort) {
        messagePort.onmessage = null;
      }
    };
  }, [messagePort]);

  return <div className="h-full bg-black" ref={containerRef} />;
}
