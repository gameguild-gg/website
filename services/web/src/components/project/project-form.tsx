'use client';

import React, { useState } from 'react';
import { SubmitButton } from '@/components/ui/submit-button';
import {
  Card,
  CardContent,
  CardFooter,
  CardHeader,
  CardTitle,
} from '../ui/card';
import { Input } from '../ui/input';
import { Label } from '../ui/label';
import { useRouter } from 'next/navigation';
import { Api, ProjectApi } from '@game-guild/apiclient';
import ApiErrorResponseDto = Api.ApiErrorResponseDto;
import slugify from 'slugify';
import { useToast } from '@/components/ui/use-toast';
import { getSession } from 'next-auth/react';
import Image from 'next/image';

import { Button } from '@/components/ui/button';
import { Textarea } from '@/components/ui/textarea';
import { Select } from '@/components/ui/select';
import { Checkbox } from '@/components/ui/checkbox';
import { Upload, DollarSign, Tag, Users, X, Plus } from 'lucide-react';
import { FileUploader } from '@/components/ui/file-uploader';

export interface ProjectFormProps {
  action: 'create' | 'update';
  slug?: string;
}

export type ImageOrFile = File | Api.ImageEntity;

// converts an ImageOrFile to an url
const imageOrFileToUrl = (imageOrFile: ImageOrFile) => {
  if (imageOrFile instanceof File) {
    return URL.createObjectURL(imageOrFile as File);
  }
  const image = imageOrFile as Api.ImageEntity;
  // todo: add the full path to the image using the api url
  return `${image.path}/${image.filename}`;
};

// reference: https://itch.io/game/new

// receive params to specify if the form is for creating or updating a project
export default function ProjectForm({
  action,
  slug,
}: Readonly<ProjectFormProps>) {
  const [project, setProject] = React.useState<Api.ProjectEntity | null>();
  const [errorApi, setErrorApi] = React.useState<ApiErrorResponseDto | null>();
  const router = useRouter();
  const toast = useToast();

  const [bannerImage, setBannerImage] = useState<ImageOrFile | null>(null);
  const [videoUrl, setVideoUrl] = useState('');
  const [screenshots, setScreenshots] = useState<ImageOrFile[]>([]);

  const getYouTubeId = (url: string) => {
    const match = url.match(
      /(?:https?:\/\/)?(?:www\.)?(?:youtube\.com|youtu\.be)\/(?:watch\?v=)?(.+)/,
    );
    return match ? match[1] : '';
  };

  const removeScreenshot = (index: number) => {
    setScreenshots(screenshots.filter((_, i) => i !== index));
  };

  const handleScreenshotUpload = (e: React.ChangeEvent<HTMLInputElement>) => {
    if (e.target.files && e.target.files[0]) {
      setScreenshots([...screenshots, e.target.files[0]]);
    }
  };

  const fetchProject = async () => {
    if (!slug) {
      return;
    }
    const session = await getSession();
    const api = new ProjectApi({
      basePath: process.env.NEXT_PUBLIC_API_URL,
    });
    const response = await api.getOneBaseProjectControllerProjectEntity(
      { slug: slug },
      {
        headers: { Authorization: `Bearer ${session?.user?.accessToken}` },
      },
    );
    if (response.status === 200) {
      setProject(response.body as Api.ProjectEntity);
      setBannerImage(response.body.thumbnail as ImageOrFile);
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
      //setErrors(response.body as ApiErrorResponseDto);
    }
  };

  useState(() => {
    if (action === 'update' && slug) {
      fetchProject();
    }
  });

  const createProject = async () => {
    toast.toast({ title: 'Creating project' });
    const session = await getSession();
    const api = new ProjectApi({
      basePath: process.env.NEXT_PUBLIC_API_URL,
    });

    const response = await api.createOneBaseProjectControllerProjectEntity(
      project,
      {
        headers: { Authorization: `Bearer ${session?.user?.accessToken}` },
      },
    );
    if (response.status === 201 || response.status === 200) {
      setProject(response.body as Api.ProjectEntity);
    } else if (response.status === 401) {
      toast.toast({ title: 'Error', description: 'Unauthorized' });
      // wait for the toast to show
      await new Promise((resolve) => setTimeout(resolve, 1000));
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
    }
  };
  const updateProject = async () => {};
  const updateImages = async () => {};

  const submit = async () => {
    if (!project) {
      toast.toast({
        title: 'Error',
        description: 'Empty project, please fill it',
      });
      return;
    }

    switch (action) {
      case 'create':
        await createProject();
        // await uploadImages();
        break;
      case 'update':
        await updateProject();
        // await uploadImages();
        break;
    }
    router.push(`/project/${project.slug}`);
  };

  // return (
  //   <Card className="w-full max-w-md">
  //     <CardHeader>
  //       <CardTitle>
  //         {action === 'create' ? 'Create Project' : 'Update Project'}
  //       </CardTitle>
  //       <CardDescription>
  //         {action === 'create'
  //           ? 'Create a new project to share with the community'
  //           : 'Update an existing project'}
  //       </CardDescription>
  //     </CardHeader>
  //     {/*<form action={formAction}>*/}
  //     <CardContent className="space-y-4">
  //       <div className="space-y-2">
  //         <Label htmlFor="title">Project Name</Label>
  //         <Input
  //           id="title"
  //           name="title"
  //           placeholder="Enter project name"
  //           value={project?.title}
  //           onChange={(e) =>
  //             setProject({
  //               ...project,
  //               title: e.target.value,
  //               slug: slugify(e.target.value, {
  //                 lower: true,
  //                 strict: true,
  //                 locale: 'en',
  //                 trim: true,
  //               }),
  //             } as Api.CreateProjectDto)
  //           }
  //           required
  //         />
  //         {/*{state.errors?.projectName && (*/}
  //         {/*  <p className="text-red-500">{state.errors.projectName}</p>*/}
  //         {/*)}*/}
  //       </div>
  //       <div className="space-y-2">
  //         <Label htmlFor="slug">Project Slug</Label>
  //         <div className="flex items-center space-x-2">
  //           <span className="text-gray-500">https://gameguild.gg/project/</span>
  //           <Input
  //             id="slug"
  //             name="slug"
  //             placeholder="Enter project name"
  //             value={project?.slug}
  //             onChange={(e) =>
  //               setProject({
  //                 ...project,
  //                 slug: slugify(e.target.value, {
  //                   trim: false,
  //                   lower: true,
  //                   strict: true,
  //                   locale: 'en',
  //                 }),
  //               } as Api.CreateProjectDto)
  //             }
  //             required
  //           />
  //         </div>
  //       </div>
  //       <div className="space-y-2">
  //         <Label htmlFor="summary">Summary</Label>
  //         <Input
  //           id="summary"
  //           name="summary"
  //           placeholder="Enter project description"
  //           value={project?.summary}
  //           onChange={(e) =>
  //             setProject({
  //               ...project,
  //               summary: e.target.value,
  //             } as Api.CreateProjectDto)
  //           }
  //           required
  //         />
  //         {/*{state.errors?.summary && (*/}
  //         {/*  <p className="text-red-500">{state.errors.summary}</p>*/}
  //         {/*)}*/}
  //       </div>
  //       <div className="space-y-2">
  //         <Label htmlFor="body">Body</Label>
  //         <Input
  //           id="body"
  //           name="body"
  //           placeholder="Enter project's body"
  //           value={project?.body}
  //           onChange={(e) =>
  //             setProject({
  //               ...project,
  //               body: e.target.value,
  //             } as Api.CreateProjectDto)
  //           }
  //           required
  //         />
  //         {/*{state.errors?.body && (*/}
  //         {/*  <p className="text-red-500">{state.errors.body}</p>*/}
  //         {/*)}*/}
  //       </div>
  //     </CardContent>
  //     <CardFooter>
  //       <SubmitButton onClick={createProject}>Create Project</SubmitButton>
  //     </CardFooter>
  //     {/*</form>*/}
  //   </Card>
  // );

  return (
    <div className="min-h-screen bg-gray-100">
      <main className="max-w-7xl mx-auto px-4 sm:px-6 lg:px-8 py-8">
        <Card>
          <CardHeader>
            <CardTitle>
              {action === 'create' ? 'Create a new Project' : 'Update Project'}
            </CardTitle>
          </CardHeader>
          <CardContent>
            <div className="space-y-6">
              <div>
                <h3 className="text-lg font-medium">
                  Make sure everyone can find your page
                </h3>
                <p className="text-sm text-gray-500">
                  Review our{' '}
                  <a href="#" className="text-blue-500 hover:underline">
                    quality guidelines
                  </a>{' '}
                  before posting your project
                </p>
              </div>
              <div>
                <Label htmlFor="title">Title</Label>
                <div className="flex items-center space-x-2">
                  <Input
                    id="title"
                    name="title"
                    placeholder="Enter project name"
                    value={project?.title}
                    onChange={(e) => {
                      // clamp the title length to 60 characters max
                      const title = e.target.value.slice(0, 60);
                      setProject({
                        ...project,
                        title: title,
                        slug: slugify(title, {
                          lower: true,
                          strict: true,
                          locale: 'en',
                          trim: true,
                        }),
                      } as Api.ProjectEntity);
                    }}
                    required
                  />
                  <span>{project?.title?.length || 0}/60</span>
                </div>
                <p className="text-sm text-gray-500 mt-1">
                  Maximum length: 60 characters
                </p>
              </div>
              <div>
                <Label htmlFor="slug">Project URL slug</Label>
                <div className="flex items-center space-x-2">
                  <span>https://gameguild.gg/project/</span>
                  <Input
                    id="slug"
                    name="slug"
                    placeholder="Slugify your title"
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
                      } as Api.ProjectEntity)
                    }
                    required
                  />
                </div>
                <p className="text-sm text-gray-500 mt-1">
                  Slug will be used in the URL of your project page
                </p>
              </div>

              <div>
                <Label htmlFor="banner">Banner Image</Label>
                <FileUploader
                  id="banner"
                  accept="image/*"
                  onFileSelect={(file) => setBannerImage(file)}
                />
                <p className="text-sm text-gray-500 mt-1">
                  Recommended size: 1200x400px
                </p>
                {bannerImage && (
                  <div className="relative inline-block">
                    <Image
                      // if bannerImage is a File, create a URL object, if not, use the image path url
                      src={imageOrFileToUrl(bannerImage)}
                      width={400}
                      height={133}
                      alt="Banner preview"
                      className="rounded-lg"
                    />
                    <Button
                      variant="destructive"
                      size="icon"
                      className="absolute top-2 right-2"
                      onClick={(event) => {
                        event.preventDefault();
                        setBannerImage(null);
                      }}
                    >
                      <X className="h-4 w-4" />
                    </Button>
                  </div>
                )}
              </div>
              <div>
                <Label>Screenshots</Label>
                <div className="grid grid-cols-5 gap-5 mt-4">
                  {screenshots.map((screenshot, index) => (
                    <div key={index} className="relative inline-block">
                      <Image
                        src={imageOrFileToUrl(screenshot)}
                        width={300}
                        height={300}
                        alt={`Screenshot ${index + 1}`}
                        className="rounded-lg"
                      />
                      <Button
                        variant="destructive"
                        size="icon"
                        className="absolute top-2 right-2"
                        onClick={(event) => {
                          event.preventDefault();
                          removeScreenshot(index);
                        }}
                      >
                        <X className="h-4 w-4" />
                      </Button>
                    </div>
                  ))}
                  {screenshots.length < 5 && (
                    <label
                      htmlFor="screenshot-upload"
                      className="cursor-pointer"
                    >
                      <div className="w-full h-[150px] border-2 border-dashed border-gray-300 rounded-lg flex items-center justify-center">
                        <Plus className="h-12 w-12 text-gray-400" />
                      </div>
                      <input
                        id="screenshot-upload"
                        type="file"
                        accept="image/*"
                        className="hidden"
                        onChange={handleScreenshotUpload}
                      />
                    </label>
                  )}
                </div>
                <p className="text-sm text-gray-500 mt-1">
                  Upload up to 5 screenshots. They will appear on your game's
                  page. Optional but highly recommended.
                </p>
              </div>
              {/*<div>*/}
              {/*  <Label htmlFor="gameplay-video">*/}
              {/*    Gameplay video or trailer*/}
              {/*  </Label>*/}
              {/*  <Input*/}
              {/*    id="gameplay-video"*/}
              {/*    placeholder="e.g. https://www.youtube.com/watch?v=dQw4w9WgXcQ"*/}
              {/*    onChange={(e) => setVideoUrl(e.target.value)}*/}
              {/*  />*/}
              {/*  <p className="text-sm text-gray-500 mt-1">*/}
              {/*    Provide a link to YouTube or Vimeo.*/}
              {/*  </p>*/}
              {/*  {videoUrl && (*/}
              {/*    <div className="mt-4 aspect-video">*/}
              {/*      <iframe*/}
              {/*        width="100%"*/}
              {/*        height="100%"*/}
              {/*        src={`https://www.youtube.com/embed/${getYouTubeId(videoUrl)}`}*/}
              {/*        frameBorder="0"*/}
              {/*        allow="accelerometer; autoplay; clipboard-write; encrypted-media; gyroscope; picture-in-picture"*/}
              {/*        allowFullScreen*/}
              {/*      ></iframe>*/}
              {/*    </div>*/}
              {/*  )}*/}
              {/*</div>*/}
              <div>
                <Label htmlFor="short-description">
                  Short description or tagline.
                </Label>
                <div className="flex items-center space-x-2">
                  <Input
                    id="short-description"
                    placeholder="Optional"
                    value={project?.summary}
                    onChange={(e) => {
                      const summary = e.target.value.slice(0, 140);
                      setProject({
                        ...project,
                        summary: summary,
                      } as Api.ProjectEntity);
                    }}
                  />
                  <span>{project?.summary?.length || 0}/140</span>
                </div>
                <p className="text-sm text-gray-500 mt-1">
                  Shown when share to your project on social medias. Maximum
                  length: 140 characters.
                </p>
              </div>
              {/*<div>*/}
              {/*  <Label htmlFor="classification">Classification</Label>*/}
              {/*  <Select>*/}
              {/*    <option>Games — A piece of software you can play</option>*/}
              {/*  </Select>*/}
              {/*</div>*/}
              {/*<div>*/}
              {/*  <Label htmlFor="kind-of-project">Kind of project</Label>*/}
              {/*  <Select>*/}
              {/*    <option>*/}
              {/*      Downloadable — You only have files to be downloaded*/}
              {/*    </option>*/}
              {/*  </Select>*/}
              {/*</div>*/}
              {/*<div>*/}
              {/*  <Label htmlFor="release-status">Release status</Label>*/}
              {/*  <Select>*/}
              {/*    <option>*/}
              {/*      Released — Project is complete, but might receive some*/}
              {/*      updates*/}
              {/*    </option>*/}
              {/*  </Select>*/}
              {/*</div>*/}
              {/*<div>*/}
              {/*  <h3 className="text-lg font-medium mb-2">Pricing</h3>*/}
              {/*  <div className="flex space-x-4">*/}
              {/*    <div className="flex items-center">*/}
              {/*      <Checkbox id="free-or-donate" />*/}
              {/*      <label htmlFor="free-or-donate" className="ml-2">*/}
              {/*        $0 or donate*/}
              {/*      </label>*/}
              {/*    </div>*/}
              {/*    <div className="flex items-center">*/}
              {/*      <Checkbox id="paid" />*/}
              {/*      <label htmlFor="paid" className="ml-2">*/}
              {/*        Paid*/}
              {/*      </label>*/}
              {/*    </div>*/}
              {/*    <div className="flex items-center">*/}
              {/*      <Checkbox id="no-payments" />*/}
              {/*      <label htmlFor="no-payments" className="ml-2">*/}
              {/*        No payments*/}
              {/*      </label>*/}
              {/*    </div>*/}
              {/*  </div>*/}
              {/*  <p className="text-sm text-gray-500 mt-2">*/}
              {/*    Someone downloading your project will be asked for a*/}
              {/*    donation before getting access. They can skip to download*/}
              {/*    for free.*/}
              {/*  </p>*/}
              {/*</div>*/}
              {/*<div>*/}
              {/*  <Label htmlFor="suggested-donation">Suggested donation</Label>*/}
              {/*  <div className="relative">*/}
              {/*    <DollarSign className="absolute left-3 top-1/2 transform -translate-y-1/2 text-gray-400" />*/}
              {/*    <Input*/}
              {/*      id="suggested-donation"*/}
              {/*      className="pl-8"*/}
              {/*      placeholder="2.00"*/}
              {/*    />*/}
              {/*  </div>*/}
              {/*  <p className="text-sm text-gray-500 mt-1">*/}
              {/*    Default donation amount*/}
              {/*  </p>*/}
              {/*</div>*/}
              {/*<div>*/}
              {/*  <h3 className="text-lg font-medium mb-2">Game Files</h3>*/}
              {/*  <FileUploader accept=".zip,.rar,.7zip" />*/}
              {/*  <p className="text-sm text-gray-500 mt-1">*/}
              {/*    Upload your game files here. Accepted formats: .zip, .rar,*/}
              {/*    .7zip*/}
              {/*  </p>*/}
              {/*</div>*/}
              <div>
                <Label htmlFor="body">Body</Label>
                <Textarea
                  id="body"
                  rows={6}
                  onChange={(e) =>
                    setProject({
                      ...project,
                      body: e.target.value,
                    } as Api.ProjectEntity)
                  }
                />
                <p className="text-sm text-gray-500 mt-1">
                  This will make up the content of your game page.
                </p>
              </div>
              {/*<div>*/}
              {/*  <Label htmlFor="genre">Genre</Label>*/}
              {/*  <Select>*/}
              {/*    <option>No genre</option>*/}
              {/*  </Select>*/}
              {/*  <p className="text-sm text-gray-500 mt-1">*/}
              {/*    Select the category that best describes your game. You can*/}
              {/*    pick additional genres with tags below*/}
              {/*  </p>*/}
              {/*</div>*/}
              {/*<div>*/}
              {/*  <Label htmlFor="tags">Tags</Label>*/}
              {/*  <div className="relative">*/}
              {/*    <Tag className="absolute left-3 top-1/2 transform -translate-y-1/2 text-gray-400" />*/}
              {/*    <Input*/}
              {/*      id="tags"*/}
              {/*      className="pl-8"*/}
              {/*      placeholder="Click to add custom tags, type to filter or enter custom tag"*/}
              {/*    />*/}
              {/*  </div>*/}
              {/*  <p className="text-sm text-gray-500 mt-1">*/}
              {/*    Any other keywords someone might search to find your game.*/}
              {/*    Max of 10. Avoid using the genre or platforms provided*/}
              {/*    above.*/}
              {/*  </p>*/}
              {/*</div>*/}
              {/*<div>*/}
              {/*  <h3 className="text-lg font-medium mb-2">Community</h3>*/}
              {/*  <p className="text-sm text-gray-500 mb-2">*/}
              {/*    Build a community for your project by letting people post to*/}
              {/*    your page.*/}
              {/*  </p>*/}
              {/*  <div className="space-y-2">*/}
              {/*    <div className="flex items-center">*/}
              {/*      <Checkbox id="community-disabled" />*/}
              {/*      <label htmlFor="community-disabled" className="ml-2">*/}
              {/*        Disabled*/}
              {/*      </label>*/}
              {/*    </div>*/}
              {/*    <div className="flex items-center">*/}
              {/*      <Checkbox id="community-comments" />*/}
              {/*      <label htmlFor="community-comments" className="ml-2">*/}
              {/*        Comments — Add a nested comment thread to the bottom of*/}
              {/*        the project page*/}
              {/*      </label>*/}
              {/*    </div>*/}
              {/*    <div className="flex items-center">*/}
              {/*      <Checkbox id="community-board" />*/}
              {/*      <label htmlFor="community-board" className="ml-2">*/}
              {/*        Discussion board — Add a dedicated community page with*/}
              {/*        categories, threads, replies & more*/}
              {/*      </label>*/}
              {/*    </div>*/}
              {/*  </div>*/}
              {/*</div>*/}
              {/*<div>*/}
              {/*  <h3 className="text-lg font-medium mb-2">*/}
              {/*    Visibility & access*/}
              {/*  </h3>*/}
              {/*  <p className="text-sm text-gray-500 mb-2">*/}
              {/*    Use Draft to review your page before making it public.{' '}*/}
              {/*    <a href="#" className="text-blue-500 hover:underline">*/}
              {/*      Learn more about access modes*/}
              {/*    </a>*/}
              {/*  </p>*/}
              {/*  <div className="space-y-2">*/}
              {/*    <div className="flex items-center">*/}
              {/*      <Checkbox id="visibility-draft" />*/}
              {/*      <label htmlFor="visibility-draft" className="ml-2">*/}
              {/*        Draft — Only those who can edit the project can view the*/}
              {/*        page*/}
              {/*      </label>*/}
              {/*    </div>*/}
              {/*    <div className="flex items-center">*/}
              {/*      <Checkbox id="visibility-restricted" />*/}
              {/*      <label htmlFor="visibility-restricted" className="ml-2">*/}
              {/*        Restricted — Only certain & authorized people can view*/}
              {/*        the page*/}
              {/*      </label>*/}
              {/*    </div>*/}
              {/*    <div className="flex items-center">*/}
              {/*      <Checkbox id="visibility-public" />*/}
              {/*      <label htmlFor="visibility-public" className="ml-2">*/}
              {/*        Public — Anyone can view the page, you can enable this*/}
              {/*        after you've saved*/}
              {/*      </label>*/}
              {/*    </div>*/}
              {/*  </div>*/}
              {/*</div>*/}
              <CardFooter>
                <Button className="w-full" onClick={submit}>
                  Save & view page
                </Button>
              </CardFooter>
            </div>
          </CardContent>
        </Card>
      </main>
    </div>
  );
}
