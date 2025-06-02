type Props = {
  params: {
    slug: string;
  };
};

import React from 'react';
import ProjectForm from '@/components/projects/project-form';

export default function Component({ params: { slug } }: Readonly<Props>) {
  return <ProjectForm action={'update'} slug={slug} />;
}
