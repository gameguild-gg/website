'use client';

import React, { useState, useEffect } from 'react';
import { CookieIcon } from 'lucide-react';

type CookieConsentProps = {};

export default function CookieConsent({}: Readonly<CookieConsentProps>) {
  const [visible, setVisible] = useState(true);

  useEffect(() => {
    //if (localStorage.getItem('cookie_consent') == 'true'){
    //  setVisible(false);
    //}
  }, []);

  const AcceptCookies = () => {
    localStorage.setItem('cookie_consent', 'true');
    setVisible(false);
  };

  return (
    <>
      {visible && (
        <aside className="fixed inset-x-0 bottom-0 z-50 flex flex-col p-6 gap-2 bg-gray-950 text-white">
          <div className="flex items-center space-x-4 mx-auto">
            <CookieIcon className="w-8 h-8 " />
            <div className="space-y-1 text-sm">
              <h4 className="font-semibold">
                We need cookies to provide the best experience.
                <br />
                By continuing to use our site, you agree to our cookies.
              </h4>
            </div>
            <button
              onClick={AcceptCookies}
              className="border p-1 rounded hover:text-gray-950 hover:bg-white"
            >
              Accept
            </button>
          </div>
        </aside>
      )}
    </>
  );
}
