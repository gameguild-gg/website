'use client';

import React from 'react';
import SummaryPage from './summary/page';
import { SessionProvider } from 'next-auth/react';

export default function CompetitionHome() {
  return (
    <>
      <SummaryPage />
    </>
  );
}
