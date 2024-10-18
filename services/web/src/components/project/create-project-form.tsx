'use client';

import React from 'react';
import { SubmitButton } from '@/components/ui/submit-button';
import {
  Card,
  CardContent,
  CardDescription,
  CardFooter,
  CardHeader,
  CardTitle,
} from '../ui/card';
import { Input } from '../ui/input';
import { Label } from '../ui/label';
import { useRouter } from 'next/navigation';
import { Api, ProjectApi } from '@game-guild/apiclient';
import { getSession } from 'next-auth/react';
import ApiErrorResponseDto = Api.ApiErrorResponseDto;
import slugify from 'slugify';

export default function CreateProjectForm() {
  const [project, setProject] = React.useState<Api.CreateProjectDto | null>();
  const [errors, setErrors] = React.useState<ApiErrorResponseDto | null>();
  const router = useRouter();

  const createProject = async () => {
    if (project) {
      const session = await getSession();
      const api = new ProjectApi({
        basePath: process.env.NEXT_PUBLIC_API_URL,
      });
      const response = await api.createOneBaseProjectControllerProjectEntity(
        project,
        { headers: { Authorization: `Bearer ${session?.user?.accessToken}` } },
      );
      if (response.status === 201) {
        // redirect to the project page
        const project = response.body as Api.ProjectEntity;
        router.push(`/projects/${project.slug}`);
      } else if (response.status === 401) {
        router.push(`/connect`);
      } else {
        setErrors(response.body as ApiErrorResponseDto);
      }
    }
  };

  return (
    <Card className="w-full max-w-md">
      <CardHeader>
        <CardTitle>Create a New Project</CardTitle>
        <CardDescription>
          Fill in the details to create a new project.
        </CardDescription>
      </CardHeader>
      {/*<form action={formAction}>*/}
      <CardContent className="space-y-4">
        {// for all constraints error, display only the error message not the keys
        errors?.message.map((m) => {
          return (
            <div key={m.property}>
              {Object.values(m.constraints).map((c) => {
                return <p className="text-red-500">{JSON.stringify(c)}</p>;
              })}
            </div>
          );
        })}
        <div className="space-y-2">
          <Label htmlFor="title">Project Name</Label>
          <Input
            id="title"
            name="title"
            placeholder="Enter project name"
            value={project?.title}
            onChange={(e) =>
              setProject({
                ...project,
                title: e.target.value,
                slug: slugify(e.target.value, {
                  lower: true,
                  strict: true,
                  locale: 'en',
                }),
              } as Api.CreateProjectDto)
            }
            required
          />
          {/*{state.errors?.projectName && (*/}
          {/*  <p className="text-red-500">{state.errors.projectName}</p>*/}
          {/*)}*/}
        </div>
        <div className="space-y-2">
          <Label htmlFor="slug">Slug</Label>
          <Input
            id="slug"
            name="slug"
            placeholder="Enter project name"
            value={project?.slug}
            onChange={(e) =>
              setProject({
                ...project,
                slug: slugify(e.target.value),
              } as Api.CreateProjectDto)
            }
            required
          />
          {/*{state.errors?.slug && (*/}
          {/*  <p className="text-red-500">{state.errors.slug}</p>*/}
          {/*)}*/}
        </div>
        <div className="space-y-2">
          <Label htmlFor="summary">Summary</Label>
          <Input
            id="summary"
            name="summary"
            placeholder="Enter project description"
            value={project?.summary}
            onChange={(e) =>
              setProject({
                ...project,
                summary: e.target.value,
              } as Api.CreateProjectDto)
            }
            required
          />
          {/*{state.errors?.summary && (*/}
          {/*  <p className="text-red-500">{state.errors.summary}</p>*/}
          {/*)}*/}
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
            placeholder="Enter project thubnail url"
            value={project?.thumbnail}
            onChange={(e) =>
              setProject({
                ...project,
                thumbnail: e.target.value,
              } as Api.CreateProjectDto)
            }
            required
          />
          {/*{state.errors?.thumbnail && (*/}
          {/*  <p className="text-red-500">{state.errors.thumbnail}</p>*/}
          {/*)}*/}
        </div>
        <div className="space-y-2">
          <Label htmlFor="body">Body</Label>
          <Input
            id="body"
            name="body"
            placeholder="Enter project's body"
            value={project?.body}
            onChange={(e) =>
              setProject({
                ...project,
                body: e.target.value,
              } as Api.CreateProjectDto)
            }
            required
          />
          {/*{state.errors?.body && (*/}
          {/*  <p className="text-red-500">{state.errors.body}</p>*/}
          {/*)}*/}
        </div>
      </CardContent>
      <CardFooter>
        <SubmitButton onClick={createProject}>Create Project</SubmitButton>
      </CardFooter>
      {/*</form>*/}
    </Card>
  );
}
