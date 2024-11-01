import React from 'react';
import ProjectForm from '@/components/project/project-form';

export default async function Page() {
  return <ProjectForm formAction={'create'} />;
}
