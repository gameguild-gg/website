'use client';

import React, { useEffect, useState } from 'react';
import { useParams, useRouter } from 'next/navigation';
import Link from 'next/link';
import { Button } from '@/components/ui/button';
//import { TicketApi } from '@game-guild/apiclient/api';
//import { getSession } from 'next-auth/react';
import { Api } from '@game-guild/apiclient/models';

interface Ticket {
  id: string;
  status: string;
  title: string;
  owner: Api.UserEntity;
  project: Api.ProjectEntity;
  owner_username: string;
  description: string;
}

// eslint-disable-next-line @typescript-eslint/explicit-function-return-type
export default function TicketDetail() {
  //const apiTicket = new TicketApi({
  //   basePath: process.env.NEXT_PUBLIC_API_URL,
  //});
  const router = useRouter();
  const { id } = useParams();
  const [ticket, setTicket] = useState<Ticket | null>(null);
  const [isLoading, setIsLoading] = useState(true);

  useEffect(() => {
    const storedTicket = localStorage.getItem('selectedTicket');
    if (storedTicket) {
      setTicket(JSON.parse(storedTicket));
    } else {
      router.push('/gtl/owner');
    }
    setIsLoading(false);
  }, [id, router]);

  if (isLoading) {
    return (
      <div className="flex justify-center items-center h-screen">
        Loading...
      </div>
    );
  }

  if (!ticket) {
    return (
      <div className="flex justify-center items-center h-screen">
        Ticket not found
      </div>
    );
  }

  return (
    <div className="bg-black text-white min-h-screen flex justify-center items-start pt-4">
      <main className="bg-gray-900 w-full max-w-3xl mx-auto px-4 sm:px-6 lg:px-8 py-6 rounded-lg shadow-lg">
        <h1 className="text-2xl font-semibold mb-4">Ticket Details</h1>
        <div className="bg-gray-800 rounded-lg p-6">
          <div className="mb-4">
            <span className="text-gray-400">Ticket ID:</span> {ticket.id}
          </div>
          <div className="mb-4">
            <span className="text-gray-400">Ticket Title:</span> {ticket.title}
          </div>
          <div className="mb-4">
            <span className="text-gray-400">Status:</span> {ticket.status}
          </div>
          <div className="mb-4">
            <span className="text-gray-400">Game:</span> {ticket.project.title}
          </div>
          <div className="mb-4">
            <span className="text-gray-400">Submitted by:</span>{' '}
            {ticket.owner.username}
          </div>
          <div className="mb-4">
            <span className="text-gray-400">Description:</span>
            <p className="mt-2 text-gray-300">{ticket.description}</p>
          </div>
        </div>
        <div className="mt-6">
          <Button asChild>
            <Link href="/gtl/owner">Back to Dashboard</Link>
          </Button>
        </div>
      </main>
    </div>
  );
}
