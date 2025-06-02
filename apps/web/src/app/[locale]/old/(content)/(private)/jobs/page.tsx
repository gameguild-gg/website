'use client';
import { useEffect, useState } from 'react';
import { useRouter } from 'next/navigation';
import { getSession } from 'next-auth/react';
import { Api, JobApplicationsApi, JobPostsApi, JobTagsApi } from '@game-guild/apiclient';
import { Button } from '@/components/ui/button';
import { Input } from '@/components/ui/input';
import { Card, CardContent } from '@/components/ui/card';
import { Avatar, AvatarFallback, AvatarImage } from '@/components/ui/avatar';
import { Badge } from '@/components/ui/badge';
import { ScrollArea } from '@/components/ui/scroll-area';
import { Check, Search } from 'lucide-react';
import { Command, CommandEmpty, CommandGroup, CommandInput, CommandItem, CommandList } from '@/components/ui/command';
import { Popover, PopoverContent, PopoverTrigger } from '@/components/ui/popover';
import { useToast } from '@/components/ui/use-toast';
import Link from 'next/link';
import {
  Pagination,
  PaginationContent,
  PaginationEllipsis,
  PaginationItem,
  PaginationLink,
  PaginationNext,
  PaginationPrevious,
} from '@/components/ui/pagination';

const job_types = [
  { value: 'CONTINUOUS', label: 'Continuous' },
  { value: 'TASK', label: 'Task' },
];

export default function JobBoard() {
  const [searchBarValue, setSearchBarValue] = useState<string>('');
  const [jobs, setJobs] = useState<Api.JobPostEntity[] | any>([]);
  const [jobTags, setJobTags] = useState<Api.JobTagEntity[] | any>([]);
  const [selectedJob, setSelectedJob] = useState<Api.JobPostEntity | any>();
  const [selectedJobTypes, setSelectedJobTypes] = useState<string[]>([]);
  const [selectedTags, setSelectedTags] = useState<string[]>([]);
  const [totalPages, setTotalPages] = useState<number>(1);

  const { toast } = useToast();
  const router = useRouter();

  const jobPostsApi = new JobPostsApi({
    basePath: process.env.NEXT_PUBLIC_API_URL,
  });
  const jobTagsApi = new JobTagsApi({
    basePath: process.env.NEXT_PUBLIC_API_URL,
  });
  const jobApplicationApi = new JobApplicationsApi({
    basePath: process.env.NEXT_PUBLIC_API_URL,
  });

  useEffect(() => {
    getAllJobTags();
  }, []);

  // On search parameters change
  useEffect(() => {
    // TODO: Create Overritable timer to await until the user is done selecting options to avoid sending several useless API calls
    searchJobs();
  }, [searchBarValue, selectedJobTypes, selectedTags]);

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
      setJobTags(response.body as Api.JobTagEntity[] || []);
    }
  };

  const getAllJobs = async () => {
    const session: any = await getSession();
    if (!session) {
      router.push('/connect');
      return;
    }

    const response = await jobPostsApi.jobPostControllerGetManyWithApplied(
      {
        // TODO add 'fields' prperty here to to filter fields and improve performance
        // TODO create a pagination or 'Load More' system for handling job ammounts over the limit.
        limit: 50,
      },
      { headers: { Authorization: `Bearer ${session.user.accessToken}` } },
    );
    if (response.status == 200) {
      // console.log('All jobs body:\n', response.body);
      setJobs(response.body ?? []);
      setSelectedJob(jobs[0] || null);
    } else {
      toast({
        variant: 'destructive',
        title: 'Error',
        description: 'Error searching for jobs.', // + JSON.stringify(response.body),
      });
    }
  };

  const searchJobs = async () => {
    const session: any = await getSession();
    if (!session) {
      router.push('/connect');
      return;
    }
    // building filter parameters.
    const filter: string[] = [];
    if (searchBarValue.length > 0) {
      filter.push('title||$cont||' + searchBarValue);
    }
    if (selectedJobTypes.length > 0) {
      filter.push('job_type||$eq||' + selectedJobTypes);
    }
    if (selectedTags.length > 0) {
      filter.push('job_tags.name||$in||' + selectedTags);
    }
    // console.log("filter: ",filter)
    const response = await jobPostsApi.jobPostControllerGetManyWithApplied(
      {
        filter: filter,
        limit: 50,
      },
      {
        headers: { Authorization: `Bearer ${session.user.accessToken}` },
      },
    );
    if (response.status == 200) {
      // console.log('SearchJobs response body:', response.body);
      setJobs(response.body ?? []);
      setSelectedJob(jobs[0] || null);
    } else {
      toast({
        variant: 'destructive',
        title: 'Error',
        description: 'Error searching for jobs.', // + JSON.stringify(response.body),
      });
    }
  };

  const handleSearchChange = async (
    event: React.ChangeEvent<HTMLInputElement>,
  ) => {
    setSearchBarValue(event.target.value);
    if (searchBarValue.length > 3) {
      searchJobs();
    }
    if (searchBarValue.length == 0) {
      getAllJobs();
    }
  };

  const handleApply = async (selectedJob: Api.JobPostEntity) => {
    const session: any = await getSession();
    if (!session) {
      router.push('/connect');
      return;
    }
    const response =
      await jobApplicationApi.createOneBaseJobApplicationControllerJobApplicationEntity(
        {
          job: selectedJob,
        },
        { headers: { Authorization: `Bearer ${session.user.accessToken}` } },
      );
    if (response.status == 201) {
      toast({
        title: 'Sucess!',
        description: 'Job was aplied to sucessfully.', // + JSON.stringify(response.body),
      });
      (selectedJob as any).applied = true;
    } else {
      toast({
        variant: 'destructive',
        title: 'Error',
        description: 'Error aplying for the a job.', // + JSON.stringify(response.body),
      });
    }
  };

  const handleLearnMoreButton = (slug: string) => {
    router.push('/jobs/my-applications/' + slug);
  };

  const timeAgo = (dateString: string): string => {
    const now = new Date();
    const pastDate = new Date(dateString);
    const diff = now.getTime() - pastDate.getTime(); // Time difference in milliseconds

    const seconds = Math.floor(diff / 1000);
    const minutes = Math.floor(seconds / 60);
    const hours = Math.floor(minutes / 60);
    const days = Math.floor(hours / 24);
    const months = Math.floor(days / 30);
    const years = Math.floor(days / 365);

    if (years > 0) return `${years} yr${years > 1 ? 's' : ''} ago`;
    if (months > 0) return `${months} mon${months > 1 ? 's' : ''} ago`;
    if (days > 0) return `${days} day${days > 1 ? 's' : ''} ago`;
    if (hours > 0) return `${hours} hr${hours > 1 ? 's' : ''} ago`;
    if (minutes > 0) return `${minutes} min${minutes > 1 ? 's' : ''} ago`;
    return `${seconds} sec${seconds > 1 ? 's' : ''} ago`;
  };

  return (
    <div className="min-h-[calc(100vh-80px)] bg-gray-100">
      <div className="container mx-auto p-4">
        <div className="mb-6 flex space-x-4">
          <div className="relative flex-grow">
            <Input
              value={searchBarValue}
              onChange={handleSearchChange}
              className="pl-10"
              placeholder="Search jobs..."
            />
            <Search className="absolute left-3 top-1/2 h-4 w-4 -translate-y-1/2 transform text-gray-400" />
          </div>
          {/* Job Type */}
          <Popover>
            <PopoverTrigger asChild>
              <Button variant="outline" className="w-[250px] justify-start">
                {selectedJobTypes.length > 0
                  ? `${selectedJobTypes}`
                  : 'Job Types'}
              </Button>
            </PopoverTrigger>
            <PopoverContent className="w-[200px] p-0">
              <Command>
                {/*<CommandInput placeholder="Search type..." />*/}
                <CommandList>
                  <CommandEmpty>No tags found.</CommandEmpty>
                  <CommandGroup>
                    {job_types.map((type) => (
                      <CommandItem
                        key={type.value}
                        onSelect={() => {
                          setSelectedJobTypes((prev) =>
                            prev.includes(type.value)
                              ? prev.filter((t) => t !== type.value)
                              : [...prev, type.value],
                          );
                        }}
                      >
                        <div
                          className={`mr-2 flex h-4 w-4 items-center justify-center rounded-sm border border-primary ${
                            selectedJobTypes.includes(type.value)
                              ? 'bg-primary text-primary-foreground'
                              : 'opacity-50 [&_svg]:invisible'
                          }`}
                        >
                          <Check className={`h-4 w-4`} />
                        </div>
                        {type.label}
                      </CommandItem>
                    ))}
                  </CommandGroup>
                </CommandList>
              </Command>
            </PopoverContent>
          </Popover>
          {/* Job Skillset */}
          <Popover>
            <PopoverTrigger asChild>
              <Button
                variant="outline"
                className="w-[250px] justify-start"
              >
                {selectedTags.length > 0 ? `${selectedTags}` : 'Skills'}
              </Button>
            </PopoverTrigger>
            <PopoverContent className="w-[200px] p-0">
              <Command>
                <CommandInput placeholder="Search skill..." />
                <CommandList>
                  <CommandEmpty>No tags found.</CommandEmpty>
                  <CommandGroup>
                    {jobTags || jobTags?.map((tag: Api.JobTagEntity) => (
                      <CommandItem
                        key={tag.id}
                        onSelect={() => {
                          setSelectedTags((prev) =>
                            prev.includes(tag.name)
                              ? prev.filter((t) => t !== tag.name)
                              : [...prev, tag.name],
                          );
                        }}
                      >
                        <div
                          className={`mr-2 flex h-4 w-4 items-center justify-center rounded-sm border border-primary overflow-auto ${
                            selectedTags.includes(tag.name)
                              ? 'bg-primary text-primary-foreground'
                              : 'opacity-50 [&_svg]:invisible'
                          }`}
                        >
                          <Check className={`h-4 w-4`} />
                        </div>
                        {tag.name}
                      </CommandItem>
                    ))}
                  </CommandGroup>
                </CommandList>
              </Command>
            </PopoverContent>
          </Popover>
        </div>
        <div className="grid grid-cols-1 gap-6 md:grid-cols-3">
          <div className="md:col-span-1">
            <ScrollArea className="h-[calc(100vh-200px)]">
              {(jobs.length == 0) &&
                <div>There are no available jobs at this time.</div>
              }
              {jobs.map((job) => (
                <Card
                  key={job.id}
                  className={`mb-4 cursor-pointer ${selectedJob?.id === job.id ? 'border-primary' : ''}`}
                  onClick={() => setSelectedJob(job)}
                >
                  <CardContent className="flex items-start space-x-4 p-4">
                    <Avatar>
                      <AvatarImage src={job.thumbnail} alt={job?.title} />
                      <AvatarFallback>{job?.title[0]}</AvatarFallback>
                    </Avatar>
                    <div>
                      <h3 className="font-semibold">{job.title}</h3>
                      <p className="text-sm text-gray-500">
                        {job?.owner?.email}
                      </p>
                      <p className="text-sm text-gray-500">{job?.location}</p>
                      <p className="text-sm text-gray-500">
                        {timeAgo(job?.createdAt)}
                      </p>
                    </div>
                  </CardContent>
                </Card>
              ))}
              {/* Pagination */}
              <div className="flex justify-center mt-5">
                <Pagination>
                  <PaginationContent>
                    <PaginationItem>
                      <PaginationPrevious href="#" />
                    </PaginationItem>
                    {[...Array(totalPages)].map((_, i) => (
                      <PaginationItem key={i}>
                        <PaginationLink href="#" isActive={i === 0}>
                          {i + 1}
                        </PaginationLink>
                      </PaginationItem>
                    )).slice(0, 3)}
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
            </ScrollArea>
          </div>
          <div className="md:col-span-2">
            {selectedJob && (
              <Card>
                <CardContent className="p-6">
                  <div className="mb-4 flex items-center space-x-4">
                    <Avatar>
                      <AvatarImage
                        src={selectedJob?.thumbnail}
                        alt={selectedJob?.owner?.email}
                      />
                      <AvatarFallback>
                        {selectedJob?.owner?.email[0]}
                      </AvatarFallback>
                    </Avatar>
                    <div>
                      <h2 className="text-2xl font-bold">
                        {selectedJob.title}
                      </h2>
                      <p className="text-gray-500">
                        {selectedJob?.owner?.email}
                      </p>
                    </div>
                  </div>
                  <p className="mb-4 text-gray-500">
                    {selectedJob.location} • {timeAgo(selectedJob.createdAt)}
                  </p>
                  {selectedJob?.job_tags.length > 0 && (
                    <div>
                      <p className="mb-2 text-lg font-semibold">Skills</p>
                      <div className="mb-4">
                        {selectedJob.job_tags.map((tag: any) => (
                          <Badge key={tag.id} className="mr-2 mb-2">
                            {tag.name}
                          </Badge>
                        ))}
                      </div>
                    </div>
                  )}
                  <p className="mb-2 text-lg font-semibold">Description</p>
                  <p className="mb-6">{selectedJob.body}</p>
                  {selectedJob.applied && (
                    <p className="mb-6 bg-gray-100 flex">
                      Already Applied.
                      <Link className="ml-2 font-semibold text-blue-500 hover:underline cursor-pointer"
                            href={'/jobs/my-applications/' + selectedJob.slug}>Learn more.</Link>
                    </p>
                  )}
                  {!selectedJob.applied && (
                    <Button onClick={() => handleApply(selectedJob)}>
                      Apply Now
                    </Button>
                  )}
                </CardContent>
              </Card>
            )}

          </div>

        </div>
      </div>
    </div>
  );
}
