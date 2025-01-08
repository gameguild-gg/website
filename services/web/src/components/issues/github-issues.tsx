'use client';

import { useState, useEffect, useCallback } from 'react';
import { useRouter, useSearchParams } from 'next/navigation';
import { Button } from '@/components/ui/button';
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from '@/components/ui/select';
import {
  Table,
  TableBody,
  TableCell,
  TableHead,
  TableHeader,
  TableRow,
} from '@/components/ui/table';
import { Avatar, AvatarFallback, AvatarImage } from '@/components/ui/avatar';
import Link from 'next/link';
import {
  ChevronLeft,
  ChevronRight,
  Bug,
  Lightbulb,
  HelpCircle,
} from 'lucide-react';
import {
  Tooltip,
  TooltipContent,
  TooltipProvider,
  TooltipTrigger,
} from '@/components/ui/tooltip';

interface User {
  login: string;
  avatar_url: string;
  html_url: string;
}

interface Label {
  name: string;
  color: string;
}

interface Issue {
  number: number;
  title: string;
  user: User;
  assignees: User[];
  labels: Label[];
  reactions: {
    total_count: number;
  };
  html_url: string;
  pull_request?: any;
  created_at: string;
  state: 'open' | 'closed';
}

export default function GitHubIssues() {
  const router = useRouter();
  const searchParams = useSearchParams();

  const [issues, setIssues] = useState<Issue[]>([]);
  const [assignees, setAssignees] = useState<string[]>([]);
  const [labels, setLabels] = useState<Label[]>([]);
  const [selectedAssignee, setSelectedAssignee] = useState(
    searchParams.get('assignee') || 'all',
  );
  const [selectedLabels, setSelectedLabels] = useState<string[]>(
    searchParams.get('labels')?.split(',').filter(Boolean) || [],
  );
  const [sort, setSort] = useState(searchParams.get('sort') || 'newest');
  const [isLoading, setIsLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);
  const [pageSize, setPageSize] = useState(
    Number(searchParams.get('pageSize')) || 25,
  );
  const [currentPage, setCurrentPage] = useState(
    Number(searchParams.get('page')) || 1,
  );
  const [issueType, setIssueType] = useState(
    searchParams.get('issueType') || 'issue',
  );
  const [issueState, setIssueState] = useState(
    searchParams.get('state') || 'open',
  );
  const [totalPages, setTotalPages] = useState(1);
  const [totalIssues, setTotalIssues] = useState(0);

  const updateURL = useCallback(() => {
    const params = new URLSearchParams();
    if (issueType !== 'issue') params.set('issueType', issueType);
    if (selectedAssignee !== 'all') params.set('assignee', selectedAssignee);
    if (selectedLabels.length > 0)
      params.set('labels', selectedLabels.join(','));
    if (sort !== 'newest') params.set('sort', sort);
    if (pageSize !== 25) params.set('pageSize', pageSize.toString());
    if (currentPage !== 1) params.set('page', currentPage.toString());
    if (issueState !== 'open') params.set('state', issueState);

    router.push(`?${params.toString()}`, { scroll: false });
  }, [
    issueType,
    selectedAssignee,
    selectedLabels,
    sort,
    pageSize,
    currentPage,
    issueState,
    router,
  ]);

  const fetchIssues = useCallback(async () => {
    setIsLoading(true);
    setError(null);

    const queryParams = new URLSearchParams({
      issueType,
      assignee: selectedAssignee,
      labels: selectedLabels.join(','),
      sort,
      page: currentPage.toString(),
      pageSize: pageSize.toString(),
      state: issueState,
    });

    try {
      const response = await fetch(`/api/github-issues?${queryParams}`);
      if (!response.ok) {
        throw new Error('Failed to fetch issues');
      }
      const data = await response.json();
      if (data.error) {
        throw new Error(data.error);
      }
      setIssues(data.issues);
      setTotalPages(data.totalPages);
      setTotalIssues(data.totalIssues);

      // Extract unique assignees and labels
      const uniqueAssignees = Array.from(
        new Set(
          data.issues.flatMap(
            (issue) => issue.assignees?.map((assignee) => assignee.login) || [],
          ),
        ),
      );
      const uniqueLabels = Array.from(
        new Set(
          data.issues
            .flatMap((issue) => issue.labels || [])
            .map((label) => JSON.stringify(label)),
        ),
      )
        .map((labelString) => {
          if (typeof labelString !== 'string') {
            console.error('Unexpected non-string label:', labelString);
            return null;
          }
          try {
            return JSON.parse(labelString) as Label;
          } catch {
            console.error('Failed to parse label:', labelString);
            return null;
          }
        })
        .filter((label): label is Label => label !== null);

      setAssignees(uniqueAssignees as string[]);
      setLabels(uniqueLabels);

      updateURL();
    } catch (error) {
      console.error('Error fetching issues:', error);
      setError('Failed to fetch issues. Please try again later.');
    } finally {
      setIsLoading(false);
    }
  }, [
    issueType,
    selectedAssignee,
    selectedLabels,
    sort,
    currentPage,
    pageSize,
    issueState,
    updateURL,
  ]);

  useEffect(() => {
    fetchIssues();
  }, [fetchIssues]);

  const handleLabelClick = useCallback(
    (labelName: string) => {
      setSelectedLabels((prev) =>
        prev.includes(labelName)
          ? prev.filter((l) => l !== labelName)
          : [...prev, labelName],
      );
      setCurrentPage(1);
      fetchIssues();
    },
    [fetchIssues],
  );

  const handleAvatarClick = useCallback(
    (username: string) => {
      setSelectedAssignee((prev) => (prev === username ? 'all' : username));
      setCurrentPage(1);
      fetchIssues();
    },
    [fetchIssues],
  );

  const createIssueUrl = (template: string) => {
    return `https://github.com/gameguild-gg/website/issues/new?assignees=&labels=&projects=&template=${template}.md&title=`;
  };

  if (isLoading) {
    return <div className="text-center py-10">Loading issues...</div>;
  }

  if (error) {
    return <div className="text-center py-10 text-red-500">{error}</div>;
  }

  return (
    <div className="container mx-auto p-4">
      <h1 className="text-2xl font-bold mb-4">GitHub Issues</h1>
      <div className="mb-4 flex flex-wrap gap-2">
        <Button
          onClick={() => window.open(createIssueUrl('bug_report'), '_blank')}
        >
          <Bug className="mr-2 h-4 w-4" /> Open Bug Report
        </Button>
        <Button
          onClick={() =>
            window.open(createIssueUrl('feature_request'), '_blank')
          }
        >
          <Lightbulb className="mr-2 h-4 w-4" /> Create Feature Request
        </Button>
        <Button
          onClick={() => window.open(createIssueUrl('question'), '_blank')}
        >
          <HelpCircle className="mr-2 h-4 w-4" /> Ask a Question
        </Button>
      </div>
      <div className="mb-4 flex flex-wrap gap-2">
        <Select
          value={issueType}
          onValueChange={(value) => {
            setIssueType(value);
            setCurrentPage(1);
            fetchIssues();
          }}
        >
          <SelectTrigger className="w-[160px]">
            <SelectValue placeholder="Issue type" />
          </SelectTrigger>
          <SelectContent>
            <SelectItem value="all">🔍 All</SelectItem>
            <SelectItem value="issue">🐛 Issues Only</SelectItem>
            <SelectItem value="pr">🔀 Pull Requests Only</SelectItem>
          </SelectContent>
        </Select>
        <Select
          value={issueState}
          onValueChange={(value) => {
            setIssueState(value);
            setCurrentPage(1);
            fetchIssues();
          }}
        >
          <SelectTrigger className="w-[160px]">
            <SelectValue placeholder="Issue state" />
          </SelectTrigger>
          <SelectContent>
            <SelectItem value="open">🟢 Open</SelectItem>
            <SelectItem value="closed">🔴 Closed</SelectItem>
            <SelectItem value="all">🔍 All</SelectItem>
          </SelectContent>
        </Select>
        <Select
          value={selectedAssignee}
          onValueChange={(value) => {
            setSelectedAssignee(value);
            setCurrentPage(1);
            fetchIssues();
          }}
        >
          <SelectTrigger className="w-[200px]">
            <SelectValue>
              {selectedAssignee !== 'all' ? (
                <div className="flex items-center gap-2">
                  <Avatar className="h-6 w-6">
                    <AvatarImage
                      src={`https://github.com/${selectedAssignee}.png`}
                      alt={selectedAssignee}
                    />
                    <AvatarFallback>
                      {selectedAssignee.slice(0, 2).toUpperCase()}
                    </AvatarFallback>
                  </Avatar>
                  <span>{selectedAssignee}</span>
                </div>
              ) : (
                'All Assignees'
              )}
            </SelectValue>
          </SelectTrigger>
          <SelectContent>
            <SelectItem value="all">All Assignees</SelectItem>
            {assignees.map((assignee) => (
              <SelectItem key={assignee} value={assignee}>
                <div className="flex items-center gap-2">
                  <Avatar className="h-6 w-6">
                    <AvatarImage
                      src={`https://github.com/${assignee}.png`}
                      alt={assignee}
                    />
                    <AvatarFallback>
                      {assignee.slice(0, 2).toUpperCase()}
                    </AvatarFallback>
                  </Avatar>
                  <span>{assignee}</span>
                </div>
              </SelectItem>
            ))}
          </SelectContent>
        </Select>
        <Select
          value={sort}
          onValueChange={(value) => {
            setSort(value);
            setCurrentPage(1);
            fetchIssues();
          }}
        >
          <SelectTrigger className="w-[180px]">
            <SelectValue placeholder="Sort by" />
          </SelectTrigger>
          <SelectContent>
            <SelectItem value="newest">🆕 Newest</SelectItem>
            <SelectItem value="oldest">🏛️ Oldest</SelectItem>
            <SelectItem value="most-reactions">🔥 Most Reactions</SelectItem>
            <SelectItem value="least-reactions">❄️ Least Reactions</SelectItem>
          </SelectContent>
        </Select>
      </div>
      <div className="mb-4">
        <h2 className="text-lg font-semibold mb-2">Filter by Labels:</h2>
        <div className="flex flex-wrap gap-2">
          {labels.map((label) => (
            <button
              key={`filter-${label.name}`}
              onClick={() => handleLabelClick(label.name)}
              className={`px-2 py-1 rounded-full text-sm font-semibold ${
                selectedLabels.includes(label.name)
                  ? 'ring-2 ring-offset-2 ring-blue-500'
                  : ''
              }`}
              style={{
                backgroundColor: `#${label.color}`,
                color:
                  parseInt(label.color, 16) > 0xffffff / 2 ? '#000' : '#fff',
              }}
            >
              {label.name}
            </button>
          ))}
        </div>
      </div>
      <Table>
        <TableHeader>
          <TableRow>
            <TableHead className="w-[60px] hidden sm:table-cell">#</TableHead>
            <TableHead>Title</TableHead>
            <TableHead className="w-[100px] md:w-[120px]">People</TableHead>
            <TableHead className="hidden sm:table-cell">Labels</TableHead>
            <TableHead className="hidden xl:table-cell w-[60px]">
              Reactions
            </TableHead>
          </TableRow>
        </TableHeader>
        <TableBody>
          {issues.map((issue) => (
            <TableRow key={issue.number} className="align-top">
              <TableCell className="py-4 hidden sm:table-cell">
                {issue.number}
              </TableCell>
              <TableCell className="py-4">
                <Link
                  href={issue.html_url}
                  target="_blank"
                  rel="noopener noreferrer"
                  className="text-blue-600 hover:underline"
                >
                  {issue.title.length > 128
                    ? `${issue.title.substring(0, 125)}...`
                    : issue.title}
                </Link>
              </TableCell>
              <TableCell className="py-4">
                <div className="flex flex-wrap gap-1">
                  {[issue.user, ...issue.assignees]
                    .filter(
                      (person, index, self) =>
                        index ===
                        self.findIndex((t) => t.login === person.login),
                    )
                    .map((person) => (
                      <TooltipProvider key={`${issue.number}-${person.login}`}>
                        <Tooltip>
                          <TooltipTrigger asChild>
                            <button
                              onClick={() => handleAvatarClick(person.login)}
                              className={`relative ${selectedAssignee === person.login ? 'ring-2 ring-blue-500 rounded-full' : ''}`}
                            >
                              <Avatar className="h-6 w-6 cursor-pointer">
                                <AvatarImage
                                  src={person.avatar_url}
                                  alt={person.login}
                                />
                                <AvatarFallback>
                                  {person.login.slice(0, 2).toUpperCase()}
                                </AvatarFallback>
                              </Avatar>
                            </button>
                          </TooltipTrigger>
                          <TooltipContent>
                            <p>
                              {person.login === issue.user.login
                                ? 'Author'
                                : 'Assignee'}
                              : {person.login}
                            </p>
                          </TooltipContent>
                        </Tooltip>
                      </TooltipProvider>
                    ))}
                </div>
              </TableCell>
              <TableCell className="hidden sm:table-cell py-4">
                <div className="flex flex-wrap gap-1">
                  {issue.labels.map((label) => (
                    <button
                      key={`${issue.number}-${label.name}`}
                      onClick={() => handleLabelClick(label.name)}
                      className={`px-2 py-1 rounded-full text-xs font-semibold cursor-pointer ${
                        selectedLabels.includes(label.name)
                          ? 'ring-2 ring-offset-2 ring-blue-500'
                          : ''
                      }`}
                      style={{
                        backgroundColor: `#${label.color}`,
                        color:
                          parseInt(label.color, 16) > 0xffffff / 2
                            ? '#000'
                            : '#fff',
                      }}
                    >
                      {label.name}
                    </button>
                  ))}
                </div>
              </TableCell>
              <TableCell className="hidden xl:table-cell py-4">
                {issue.reactions.total_count}
              </TableCell>
            </TableRow>
          ))}
        </TableBody>
      </Table>
      <div className="mt-4 flex justify-between items-center">
        <div>
          Showing {(currentPage - 1) * pageSize + 1} to{' '}
          {Math.min(currentPage * pageSize, totalIssues)} of {totalIssues}{' '}
          issues
        </div>
        <div className="flex items-center space-x-2">
          <Select
            value={pageSize.toString()}
            onValueChange={(value) => {
              setPageSize(Number(value));
              setCurrentPage(1);
              fetchIssues();
            }}
          >
            <SelectTrigger className="w-[180px]">
              <SelectValue placeholder="Items per page" />
            </SelectTrigger>
            <SelectContent>
              <SelectItem value="25">25 per page</SelectItem>
              <SelectItem value="50">50 per page</SelectItem>
              <SelectItem value="100">100 per page</SelectItem>
            </SelectContent>
          </Select>
          <Button
            onClick={() => {
              setCurrentPage((prev) => Math.max(prev - 1, 1));
              fetchIssues();
            }}
            disabled={currentPage === 1}
          >
            <ChevronLeft className="mr-2 h-4 w-4" /> Previous
          </Button>
          <Button
            onClick={() => {
              setCurrentPage((prev) => Math.min(prev + 1, totalPages));
              fetchIssues();
            }}
            disabled={currentPage === totalPages}
          >
            Next <ChevronRight className="ml-2 h-4 w-4" />
          </Button>
        </div>
      </div>
    </div>
  );
}
