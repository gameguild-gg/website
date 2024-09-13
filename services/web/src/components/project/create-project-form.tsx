'use client';

import React from 'react';
import {SubmitButton} from "@/components/ui/submit-button";
import {useCreateProjectFormState} from "@/hooks/project/use-create-project-form-state";
import {Card, CardContent, CardDescription, CardFooter, CardHeader, CardTitle} from '../ui/card';
import {Input} from '../ui/input';
import {Label} from '../ui/label';

export default function CreateProjectForm() {
  const [state, formAction] = useCreateProjectFormState();

  return (
    <Card className="w-full max-w-md">
      <CardHeader>
        <CardTitle>Create a New Project</CardTitle>
        <CardDescription>Fill in the details to create a new project.</CardDescription>
      </CardHeader>
      <form action={formAction}>
        <CardContent className="space-y-4">
          <div className="space-y-2">
            <Label htmlFor="projectName">Project Name</Label>
            <Input
              id="projectName"
              placeholder="Enter project name"
              // value={projectName}
              // onChange={(e) => setProjectName(e.target.value)}
              required
            />
          </div>
          <div className="space-y-2">
            <Label htmlFor="projectName">Slug</Label>
            <Input
              id="projectName"
              placeholder="Enter project name"
              // value={projectName}
              // onChange={(e) => setProjectName(e.target.value)}
              required
            />
          </div>
          <div className="space-y-2">
            <Label htmlFor="description">Description</Label>
            <Input
              id="description"
              placeholder="Enter project description"
              // value={description}
              // onChange={(e) => setDescription(e.target.value)}
              required
            />
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
            <Label htmlFor="manager">Thumbnail Url</Label>
            <Input
              id="manager"
              placeholder="Enter project manager's name"
              // value={manager}
              // onChange={(e) => setManager(e.target.value)}
              required
            />
          </div>
        </CardContent>
        <CardFooter>
          <SubmitButton>Create Project</SubmitButton>
        </CardFooter>
      </form>
    </Card>
  );
}