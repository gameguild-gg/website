'use client'

import { useEffect, useState } from 'react'
import { useParams, useRouter } from 'next/navigation'
import Link from 'next/link'
import { Button } from '@/components/ui/button'

interface Ticket {
    id: number;
    ticketStatus: string;
    gameName: string;
    submitter: string;
    description: string;
}

export default function TicketDetail() {
    const router = useRouter();
    const { id } = useParams();
    const [ticket, setTicket] = useState<Ticket | null>(null);
    const [isLoading, setIsLoading] = useState(true);

    useEffect(() => {
        // Retrieve the ticket data from localStorage
        const storedTicket = localStorage.getItem('selectedTicket');
        if (storedTicket) {
            setTicket(JSON.parse(storedTicket));
        } else {
            // If no ticket data is found, redirect to the dashboard
            router.push('/dashboard');
        }
        setIsLoading(false);
    }, [id, router]);

    if (isLoading) {
        return <div className="flex justify-center items-center h-screen">Loading...</div>;
    }

    if (!ticket) {
        return <div className="flex justify-center items-center h-screen">Ticket not found</div>;
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
                        <span className="text-gray-400">Status:</span> {ticket.ticketStatus}
                    </div>
                    <div className="mb-4">
                        <span className="text-gray-400">Game:</span> {ticket.gameName}
                    </div>
                    <div className="mb-4">
                        <span className="text-gray-400">Submitted by:</span> {ticket.submitter}
                    </div>
                    <div className="mb-4">
                        <span className="text-gray-400">Description:</span>
                        <p className="mt-2 text-gray-300">{ticket.description}</p>
                    </div>
                </div>
                <div className="mt-6">
                    <Button asChild>
                        <Link href="/dashboard">Back to Dashboard</Link>
                    </Button>
                </div>
            </main>
        </div>
    )
}