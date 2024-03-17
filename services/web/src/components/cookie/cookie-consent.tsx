"use client";

import React from "react";

import CookieIcon from "./cookie-icon";
import { Button } from "@game-guild/ui";

type CookieConsentProps = {};

function CookieConsent({}: Readonly<CookieConsentProps>) {
  return (
    <aside className="fixed inset-x-0 bottom-0 z-50 flex flex-col p-4 gap-2 bg-gray-50/90 dark:bg-gray-900/90">
      <div className="flex items-center space-x-4">
        <CookieIcon className="w-6 h-6" />
        <div className="space-y-1 text-sm">
          <h4 className="font-semibold">Cookie consent</h4>
          <p className="text-xs">
            We use cookies to provide the best experience. By continuing to use our site, you agree to our cookies.
          </p>
        </div>
      </div>
      <div className="flex items-center space-x-4">
        <Button>Accept</Button>
        <Button>Settings</Button>
      </div>
    </aside>
  );
}

export default CookieConsent;
