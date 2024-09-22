'use client';
import React, { useState } from 'react';
import axios, { AxiosError } from 'axios';

const TicketForm: React.FC = () => {
  const [title, setTitle] = useState<string>('');
  const [description, setDescription] = useState<string>('');
  const [status, setStatus] = useState<string>('OPEN');
  const [priority, setPriority] = useState<string>('LOW');
  const [error, setError] = useState<string | null>(null);

  const handleSubmit = async (event: React.FormEvent) => {
    event.preventDefault();
    setError(null); // Reset error state

    try {
      const response = await axios.post(
        'http://localhost:8080/tickets/create-ticket',
        {
          title,
          description,
          status,
          priority,
        },
      );
      console.log('Ticket created:', response.data);
      // Clear form after successful submission
      setTitle('');
      setDescription('');
      setStatus('OPEN');
      setPriority('LOW');
    } catch (err) {
      const errorMessage =
        (err as AxiosError).response?.data?.message || 'An error occurred';
      setError(errorMessage); // Set the error message
    }
  };

  return (
    <form onSubmit={handleSubmit}>
      <input
        type="text"
        value={title}
        onChange={(e) => setTitle(e.target.value)}
        placeholder="Title"
        required
      />
      <textarea
        value={description}
        onChange={(e) => setDescription(e.target.value)}
        placeholder="Description"
        required
      />
      <button type="submit">Create Ticket</button>
      {error && <div style={{ color: 'red' }}>{error}</div>}{' '}
      {/* Render error as a string */}
    </form>
  );
};

export default TicketForm;
