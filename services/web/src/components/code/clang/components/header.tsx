import React from 'react';

import { Github } from 'lucide-react';

export default function Header() {
  return (
    <div>
      <div className="flex h-15 justify-between items-center px-5">
        <div className="flex items-center gap-2">
          <a href="https://github.com/gameguild-gg/website" target="_blank" className="leading-none">
            <Github className="h-[20px] w-[20px] text-dark-700" />
          </a>
          <h1 className="font-medium">GameGuild</h1>
        </div>
      </div>
    </div>
  );
}
