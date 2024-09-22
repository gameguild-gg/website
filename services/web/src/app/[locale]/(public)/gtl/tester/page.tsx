'use client';
import React, { useState } from 'react';
import axios from 'axios';

const CreateTicket: React.FC = () => {
  const [title, setTitle] = useState('');
  const [description, setDescription] = useState('');
  const [status, setStatus] = useState('OPEN'); // Default status
  const [priority, setPriority] = useState('LOW'); // Default priority
  const userId = 'fuck';
  const userId1 = 'fuck';

  // Replace this with the actual user ID from your auth context

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault();

    const ticketData = {
      title,
      description,
      status,
      priority,
      userId,
      userId1,
    };

    try {
      const response = await axios.post(
        'http://localhost:8080/tickets/create-ticket',
        ticketData,
      );
      console.log('Ticket created:', response.data);
    } catch (error) {
      console.error(
        'Error creating ticket:',
        error.response?.data || error.message,
      );
    }
  };

  return (
    <form onSubmit={handleSubmit}>
      <div>
        <label>Title:</label>
        <input
          type="text"
          value={title}
          onChange={(e) => setTitle(e.target.value)}
          required
        />
      </div>
      <div>
        <label>Description:</label>
        <textarea
          value={description}
          onChange={(e) => setDescription(e.target.value)}
        />
      </div>
      <div>
        <label>Status:</label>
        <select value={status} onChange={(e) => setStatus(e.target.value)}>
          <option value="OPEN">Open</option>
          <option value="IN_PROGRESS">In Progress</option>
          <option value="RESOLVED">Resolved</option>
          <option value="CLOSED">Closed</option>
        </select>
      </div>
      <div>
        <label>Priority:</label>
        <select value={priority} onChange={(e) => setPriority(e.target.value)}>
          <option value="LOW">Low</option>
          <option value="MEDIUM">Medium</option>
          <option value="HIGH">High</option>
          <option value="CRITICAL">Critical</option>
        </select>
      </div>
      <button type="submit">Create Ticket</button>
    </form>
  );
};

export default CreateTicket;
