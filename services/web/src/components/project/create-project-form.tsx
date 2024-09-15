'use client';

import React from 'react';
import {SubmitButton} from "@/components/ui/submit-button";
import {useCreateProjectFormState} from "@/hooks/project/use-create-project-form-state";
import {Card, CardContent, CardDescription, CardFooter, CardHeader, CardTitle} from '../ui/card';
import {Input} from '../ui/input';
import {Label} from '../ui/label';
import {useRouter} from "next/navigation";

export default function CreateProjectForm() {
  const [state, formAction] = useCreateProjectFormState();
  const router = useRouter();

  React.useEffect(() => {
    if (state.project) {
      // Redirecionar para a página do projeto recém-criado
      router.push(`/projects/${state.project.id}`);
    }
  }, [state.project, router]);


  return (
    <Card className="w-full max-w-md">
      <CardHeader>
        <CardTitle>Create a New Project</CardTitle>
        <CardDescription>Fill in the details to create a new project.</CardDescription>
      </CardHeader>
      <form action={formAction}>
        <CardContent className="space-y-4">
          {state.errors?.general && <p className="text-red-500">{state.errors.general}</p>}
          <div className="space-y-2">
            <Label htmlFor="title">Project Name</Label>
            <Input
              id="title"
              name="title"
              placeholder="Enter project name"
              // value={projectName}
              // onChange={(e) => setProjectName(e.target.value)}
              required
            />
            {state.errors?.projectName && <p className="text-red-500">{state.errors.projectName}</p>}
          </div>
          <div className="space-y-2">
            <Label htmlFor="slug">Slug</Label>
            <Input
              id="slug"
              name="slug"
              placeholder="Enter project name"
              // value={projectName}
              // onChange={(e) => setProjectName(e.target.value)}
              required
            />
            {state.errors?.slug && <p className="text-red-500">{state.errors.slug}</p>}
          </div>
          <div className="space-y-2">
            <Label htmlFor="summary">Summary</Label>
            <Input
              id="summary"
              name="summary"
              placeholder="Enter project description"
              // value={description}
              // onChange={(e) => setDescription(e.target.value)}
              required
            />
            {state.errors?.summary && <p className="text-red-500">{state.errors.summary}</p>}
          </div>
          {/*<div className="space-y-2">*/}
          {/*  <Label htmlFor="startDate">Start Date</Label>*/}
          {/*  <Input*/}
          {/*    id="startDate"*/}
          {/*    type="date"*/}
          {/*    // value={startDate}*/}
          {/*    // onChange={(e) => setStartDate(e.target.value)}*/}
          {/*    required*/}
          {/*  />*/}
          {/*</div>*/}
          <div className="space-y-2">
            <Label htmlFor="thumbnail">Thumbnail Url</Label>
            <Input
              id="thumbnail"
              name="thumbnail"
              placeholder="Enter project manager's name"
              // value={manager}
              // onChange={(e) => setManager(e.target.value)}
              required
            />
            {state.errors?.thumbnail && <p className="text-red-500">{state.errors.thumbnail}</p>}
          </div>
          <div className="space-y-2">
            <Label htmlFor="body">Body</Label>
            <Input
              id="body"
              name="body"
              placeholder="Enter project manager's name"
              // value={manager}
              // onChange={(e) => setManager(e.target.value)}
              required
            />
            {state.errors?.body && <p className="text-red-500">{state.errors.body}</p>}
          </div>
        </CardContent>
        <CardFooter>
          <SubmitButton>Create Project</SubmitButton>
        </CardFooter>
      </form>
    </Card>
  );
}