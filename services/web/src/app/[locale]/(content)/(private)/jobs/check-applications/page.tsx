'use client';
import { useEffect, useState } from 'react';
import { useRouter } from 'next/navigation';
import { getSession } from 'next-auth/react';
import { Api, JobTagsApi, JobApplicationsApi } from '@game-guild/apiclient';
import { Badge } from '@/components/ui/badge';
import { Button } from '@/components/ui/button';
import { Checkbox } from '@/components/ui/checkbox';
import { Input } from '@/components/ui/input';
import { Label } from '@/components/ui/label';
import { Check, X } from 'lucide-react';
import { Avatar, AvatarFallback, AvatarImage } from '@/components/ui/avatar';
import {
  Pagination,
  PaginationContent,
  PaginationEllipsis,
  PaginationItem,
  PaginationLink,
  PaginationNext,
  PaginationPrevious,
} from '@/components/ui/pagination';

export default function CheckApplications() {
  const [jobTags, setJobTags] = useState<Api.JobTagEntity[] | any>([]);
  const [jobApplications, setJobApplicatons] = useState<
    Api.JobApplicationEntity[]
  >([]);
  const [totalPages, setTotalPages] = useState<number>(1);

  const router = useRouter();
  const jobTagsApi = new JobTagsApi({
    basePath: process.env.NEXT_PUBLIC_API_URL,
  });
  const jobApplicationApi = new JobApplicationsApi({
    basePath: process.env.NEXT_PUBLIC_API_URL,
  });

  useEffect(() => {
    getAllJobTags();
    loadJobApplications();
  }, []);

  const getAllJobTags = async () => {
    const session: any = await getSession();
    if (!session) {
      router.push('/connect');
      return;
    }
    const response = await jobTagsApi.getManyBaseJobTagControllerJobTagEntity(
      {},
      { headers: { Authorization: `Bearer ${session.user.accessToken}` } },
    );
    if ((response.status = 200)) {
      setJobTags((response.body as Api.JobTagEntity[]) || []);
    }
  };

  const loadJobApplications = async () => {
    const session: any = await getSession();
    if (!session) {
      router.push('/connect');
      return;
    }
    const response =
      await jobApplicationApi.jobApplicationControllerMyApplications({
        headers: { Authorization: `Bearer ${session.user.accessToken}` },
      });
    // console.log('API Response:\n',response)
    if (
      response.status == 200 &&
      (response.body as Api.JobApplicationEntity[])?.length > 0
    ) {
      setJobApplicatons(response.body as Api.JobApplicationEntity[]);
      setTotalPages(Math.ceil((response.body as any[]).length / 12));
    } else {
      router.push('/connect');
    }
  };

  const handleLearnMoreButton = (slug: string) => {
    router.push('/jobs/my-applications/' + slug);
  };

  const handleGiveUpButton = async (app: Api.JobApplicationEntity) => {
    // TODO: Add Warning
    const session: any = await getSession();
    const response = await jobApplicationApi.jobApplicationControllerWithdraw(
      app,
      { headers: { Authorization: `Bearer ${session.user.accessToken}` } },
    );
    if (response.status == 200) {
      // TODO: sucess toaster
    } else {
      // TODO: error toaster
    }
  };

  return (
    <div className="min-h-[calc(100vh-70px)] bg-gray-100">
      <div className="flex container min-h-full mx-auto p-4 bg-background">
        {/* Sidebar */}
        <aside className="w-[260px] bg-muted p-5 flex flex-col space-y-4">
          <h2 className="text-2xl font-bold">Filter</h2>
          <div>
            <Label htmlFor="jobTitle">Job Title</Label>
            <Input id="jobTitle" placeholder="Enter job title" />
          </div>
          <div>
            <Label htmlFor="location">Location</Label>
            <Input id="location" placeholder="Enter location" />
          </div>
          <div>
            <Label>Tags</Label>
            <div className="grid grid-cols-2 gap-2 mt-2">
              {jobTags.map((tag: Api.JobTagEntity) => (
                <div key={tag.id} className="flex items-center space-x-2">
                  <Checkbox id={tag.id} />
                  <Label htmlFor={tag.name}>{tag.name}</Label>
                </div>
              ))}
            </div>
          </div>
        </aside>

        {/* Main content */}
        <main className="flex-1 flex-grow min-h-full p-6">
          <h1 className="text-3xl font-bold mb-6">My Job Posts</h1>
          <div className="grid grid-cols-1 md:grid-cols-2 lg:grid-cols-4 gap-6">
            {jobApplications.length > 0 &&
              jobApplications.slice(0, 16).map((app) => (
                <div
                  key={app.id}
                  className="bg-card rounded-lg shadow-md p-4 flex flex-col h-full"
                >
                  <div className="flex-grow">
                    <div className="flex items-center mb-2">
                      <Avatar>
                        {/*<AvatarImage src={app?.job?.thumbnail} alt={app?.job?.title} />*/}
                        <AvatarFallback>{app?.job?.title[0]}</AvatarFallback>
                      </Avatar>
                      <span className="font-semibold ml-2">
                        {app?.job?.title}
                      </span>
                    </div>
                    <h3 className="font-bold text-lg mb-2">
                      {app?.job?.title}
                    </h3>
                    <div className="flex flex-wrap gap-1 mb-2">
                      {app?.job?.job_tags.map((tag) => (
                        <Badge key={tag.id} variant="secondary">
                          {tag.name}
                        </Badge>
                      ))}
                    </div>
                  </div>
                  <div className="mt-auto mb-0">
                    <div className="flex justify-between mt-3">
                      <Button
                        onClick={() => handleLearnMoreButton(app?.job?.slug)}
                      >
                        Learn More
                      </Button>
                      <Button
                        onClick={() => handleGiveUpButton(app)}
                        variant="destructive"
                      >
                        Cancel
                      </Button>
                    </div>
                  </div>
                </div>
              ))}
          </div>

          {/* Pagination */}
          <div className="flex justify-center mt-auto">
            <Pagination>
              <PaginationContent>
                <PaginationItem>
                  <PaginationPrevious href="#" />
                </PaginationItem>
                {[...Array(totalPages)]
                  .map((_, i) => (
                    <PaginationItem key={i}>
                      <PaginationLink href="#" isActive={i === 0}>
                        {i + 1}
                      </PaginationLink>
                    </PaginationItem>
                  ))
                  .slice(0, 3)}
                {totalPages > 3 && (
                  <>
                    <PaginationItem>
                      <PaginationEllipsis />
                    </PaginationItem>
                    <PaginationItem>
                      <PaginationLink href="#">{totalPages}</PaginationLink>
                    </PaginationItem>
                  </>
                )}
                <PaginationItem>
                  <PaginationNext href="#" />
                </PaginationItem>
              </PaginationContent>
            </Pagination>
          </div>
        </main>
      </div>
    </div>
  );
}
