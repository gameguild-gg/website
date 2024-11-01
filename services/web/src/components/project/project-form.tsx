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
import { useToast } from '@/components/ui/use-toast';

export interface ProjectFormProps {
  action: 'create' | 'update';
  slug?: string;
}

// reference: https://itch.io/game/new

// receive params to specify if the form is for creating or updating a project
export default function ProjectForm({
  action,
  slug,
}: Readonly<ProjectFormProps>) {
  const [project, setProject] = React.useState<Api.CreateProjectDto | null>();
  const [errors, setErrors] = React.useState<ApiErrorResponseDto | null>();
  const router = useRouter();
  const toast = useToast();

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
        router.push(`/project/${project.slug}`);
      } else if (response.status === 401) {
        router.push(`/disconnect`);
      } else {
        const error = response.body as ApiErrorResponseDto;
        let message = '';
        if (error.msg) {
          message = error.msg + `;\n`;
        }
        if (error.message && typeof error.message === 'string') {
          message += error.message + `;\n`;
        }
        if (error.message && typeof error.message === 'object') {
          for (const key in error.message) {
            message += `${key}: ${error.message[key]};\n`;
          }
        }

        toast.toast({
          title: 'Error',
          description: message,
        });
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
                  trim: true,
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
          <Label htmlFor="slug">Project Slug</Label>
          <div className="flex items-center space-x-2">
            <span className="text-gray-500">https://gameguild.gg/project/</span>
            <Input
              id="slug"
              name="slug"
              placeholder="Enter project name"
              value={project?.slug}
              onChange={(e) =>
                setProject({
                  ...project,
                  slug: slugify(e.target.value, {
                    trim: false,
                    lower: true,
                    strict: true,
                    locale: 'en',
                  }),
                } as Api.CreateProjectDto)
              }
              required
            />
          </div>
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
