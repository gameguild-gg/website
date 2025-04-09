'use client';

import React, { useEffect, useState } from 'react';
import dynamic from 'next/dynamic';
import { useRunner } from '@/components/code/clang/module';

// Dynamically import components to avoid SSR issues with browser-only code
const Layout = dynamic(() => import('@/components/code/clang/components/layout'), { ssr: false });
const Header = dynamic(() => import('@/components/code/clang/components/header'), { ssr: false });
const Editor = dynamic(() => import('@/components/code/clang/components/editor'), { ssr: false });
const Terminal = dynamic(() => import('@/components/code/clang/components/terminal'), { ssr: false });

// WASM and asset files need to be loaded as static assets
const assetFiles = {
  clang: '/assets/clang.wasm',
  lld: '/assets/lld.wasm',
  memfs: '/assets/memfs.wasm',
  sysroot: '/assets/sysroot.tar',
};

// Use this flag to prevent multiple initializations
let hasInitialized = false;

export default function ClangPage() {
  // Use local state to track initialization
  const [isInitialized, setIsInitialized] = useState(false);
  // Access the init function directly from the store rather than as a selector
  const { init } = useRunner();

  // Use a ref to ensure init only happens once
  useEffect(() => {
    if (!hasInitialized) {
      console.log('Initializing clang environment...');
      // Set the flag to prevent future calls
      hasInitialized = true;
      // Call init after a short delay to ensure the component is fully mounted

      try {
        init();
        setIsInitialized(true);
        console.log('Initialization complete');
      } catch (error) {
        console.error('Error during initialization:', error);
      }
    }

    // Log the asset file paths
    console.log('Asset files:', assetFiles);
  }, []);

  return (
    <div className="h-screen">
      <Layout header={<Header />} left={<Editor />} right={<Terminal />} />
    </div>
  );
}
