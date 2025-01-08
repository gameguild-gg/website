'use client';

import { Button } from '@/components/ui/button';
import {
  DropdownMenu,
  DropdownMenuContent,
  DropdownMenuGroup,
  DropdownMenuItem,
  DropdownMenuLabel,
  DropdownMenuSeparator,
  DropdownMenuTrigger,
} from '@/components/ui/dropdown-menu';
import { Input } from '@/components/ui/input';
import { Label } from '@/components/ui/label';
import { ChevronDown, Filter, SortAsc } from 'lucide-react';
import { useSearchParams, usePathname, useRouter } from 'next/navigation';
import { useMemo } from 'react';

export function IssuesHeader() {
  const searchParams = useSearchParams();
  const pathname = usePathname();
  const { replace } = useRouter();

  const params = useMemo(
    () => new URLSearchParams(searchParams),
    [searchParams],
  );

  function handleSearch(term: string) {
    if (term) {
      params.set('q', term);
    } else {
      params.delete('q');
    }
    replace(`${pathname}?${params.toString()}`);
  }

  function handleFilter(key: string, value: string) {
    if (value === 'all') {
      params.delete(key);
    } else {
      params.set(key, value);
    }
    replace(`${pathname}?${params.toString()}`);
  }

  return (
    <div className="flex flex-col gap-4 sm:flex-row sm:items-center justify-between">
      <div className="flex flex-1 items-center gap-2">
        <Input
          placeholder="Search issues..."
          className="max-w-xs"
          defaultValue={searchParams.get('q') ?? ''}
          onChange={(e) => handleSearch(e.target.value)}
        />
        <DropdownMenu>
          <DropdownMenuTrigger asChild>
            <Button variant="outline" className="gap-2">
              <Filter className="h-4 w-4" />
              Filters
              <ChevronDown className="h-4 w-4" />
            </Button>
          </DropdownMenuTrigger>
          <DropdownMenuContent align="start" className="w-56">
            <DropdownMenuLabel>Filter by</DropdownMenuLabel>
            <DropdownMenuSeparator />
            <DropdownMenuGroup>
              <DropdownMenuItem onSelect={() => handleFilter('author', 'all')}>
                All Authors
              </DropdownMenuItem>
              <DropdownMenuItem onSelect={() => handleFilter('author', '@me')}>
                Created by me
              </DropdownMenuItem>
            </DropdownMenuGroup>
            <DropdownMenuSeparator />
            <DropdownMenuGroup>
              <DropdownMenuItem
                onSelect={() => handleFilter('assignee', 'all')}
              >
                All Assignees
              </DropdownMenuItem>
              <DropdownMenuItem
                onSelect={() => handleFilter('assignee', '@me')}
              >
                Assigned to me
              </DropdownMenuItem>
              <DropdownMenuItem
                onSelect={() => handleFilter('assignee', 'none')}
              >
                Unassigned
              </DropdownMenuItem>
            </DropdownMenuGroup>
            <DropdownMenuSeparator />
            <DropdownMenuGroup>
              <DropdownMenuItem onSelect={() => handleFilter('label', 'all')}>
                All Labels
              </DropdownMenuItem>
              <DropdownMenuItem onSelect={() => handleFilter('label', 'none')}>
                Unlabeled
              </DropdownMenuItem>
            </DropdownMenuGroup>
          </DropdownMenuContent>
        </DropdownMenu>
        <DropdownMenu>
          <DropdownMenuTrigger asChild>
            <Button variant="outline" className="gap-2">
              <SortAsc className="h-4 w-4" />
              Sort
              <ChevronDown className="h-4 w-4" />
            </Button>
          </DropdownMenuTrigger>
          <DropdownMenuContent align="start" className="w-56">
            <DropdownMenuLabel>Sort by</DropdownMenuLabel>
            <DropdownMenuSeparator />
            <DropdownMenuGroup>
              <DropdownMenuItem onSelect={() => handleFilter('sort', 'newest')}>
                Newest
              </DropdownMenuItem>
              <DropdownMenuItem onSelect={() => handleFilter('sort', 'oldest')}>
                Oldest
              </DropdownMenuItem>
              <DropdownMenuItem
                onSelect={() => handleFilter('sort', 'most-commented')}
              >
                Most commented
              </DropdownMenuItem>
              <DropdownMenuItem
                onSelect={() => handleFilter('sort', 'least-commented')}
              >
                Least commented
              </DropdownMenuItem>
              <DropdownMenuItem
                onSelect={() => handleFilter('sort', 'recently-updated')}
              >
                Recently updated
              </DropdownMenuItem>
              <DropdownMenuItem
                onSelect={() => handleFilter('sort', 'least-recently-updated')}
              >
                Least recently updated
              </DropdownMenuItem>
            </DropdownMenuGroup>
          </DropdownMenuContent>
        </DropdownMenu>
      </div>
      <Button>New Issue</Button>
    </div>
  );
}
