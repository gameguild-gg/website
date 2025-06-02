'use server';

import { revalidatePath } from 'next/cache';

export type Project = {
  id: string;
  name: string;
  status: 'not-started' | 'in-progress' | 'active' | 'on-hold';
  createdAt?: string;
  updatedAt?: string;
};

let projects: Project[] = [
  { id: '1', name: 'Website Redesign', status: 'in-progress' },
  { id: '2', name: 'Mobile App Development', status: 'not-started' },
  { id: '3', name: 'E-commerce Platform', status: 'active' },
  { id: '4', name: 'CRM Integration', status: 'on-hold' },
  { id: '5', name: 'Data Analytics Dashboard', status: 'not-started' },
  { id: '6', name: 'Cloud Migration', status: 'active' },
  { id: '7', name: 'AI Chatbot Implementation', status: 'in-progress' },
  { id: '8', name: 'Cybersecurity Upgrade', status: 'active' },
  { id: '9', name: 'IoT Device Management', status: 'not-started' },
  { id: '10', name: 'Blockchain Prototype', status: 'on-hold' },
];

export async function getProjects() {
  // In a real application, this would be a database query
  return projects;
}

export async function createProject(name: string, status: Project['status']) {
  const newProject: Project = {
    id: (projects.length + 1).toString(),
    name,
    status,
    createdAt: new Date().toISOString(),
    updatedAt: new Date().toISOString(),
  };
  projects.push(newProject);
  revalidatePath('/projects');
  return newProject;
}
