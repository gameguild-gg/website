'use client';

import * as React from 'react';
import {
  Bug,
  HelpCircle,
  Lightbulb,
  MessageCircle,
  ListChecks,
} from 'lucide-react';
import {
  Tooltip,
  TooltipContent,
  TooltipProvider,
  TooltipTrigger,
} from '@/components/ui/tooltip';
import {
  DropdownMenu,
  DropdownMenuContent,
  DropdownMenuItem,
  DropdownMenuTrigger,
} from '@/components/ui/dropdown-menu';
import { Button } from '@/components/ui/button';
import { cn } from '@/lib/utils';
import { useEffect, useState } from 'react';

interface FloatingFeedbackButtonProps {
  className?: string;
}

export function FloatingFeedbackButton({
  className,
}: FloatingFeedbackButtonProps) {
  const createIssueUrl = (template: string) => {
    let label = '';
    let title = '';
    switch (template) {
      case 'bug_report':
        label = 'bug';
        title = '[Bug Report] ';
        break;
      case 'feature_request':
        label = 'enhancement';
        title = '[Feature Request] ';
        break;
      case 'question':
        label = 'question';
        title = '[Question] ';
        break;
    }
    return `https://github.com/gameguild-gg/website/issues/new?assignees=&labels=${label}&projects=&template=${template}.md&title=${encodeURIComponent(title)}`;
  };

  const openIssuesUrl = '/issues';

  const [version, setVersion] = useState('v0.0.1');

  useEffect(() => {
    setVersion(process.env.NEXT_PUBLIC_VERSION || 'v0.0.1');
  }, []);

  return (
    <div className={cn('fixed bottom-4 right-4 z-50', className)}>
      <TooltipProvider>
        <DropdownMenu>
          <Tooltip>
            <TooltipTrigger asChild>
              <DropdownMenuTrigger asChild>
                <Button
                  size="lg"
                  className="rounded-full shadow-lg flex items-center gap-2"
                >
                  <MessageCircle className="h-5 w-5" />
                  <span className="flex flex-col items-start">
                    <span>Give Feedback</span>
                    <span className="text-xs opacity-70">{version}</span>
                  </span>
                  <span className="sr-only">Open feedback menu</span>
                </Button>
              </DropdownMenuTrigger>
            </TooltipTrigger>
            <TooltipContent side="left">
              <p>Give Feedback</p>
            </TooltipContent>
          </Tooltip>
          <DropdownMenuContent
            align="end"
            alignOffset={-8}
            className="w-[200px]"
          >
            <DropdownMenuItem
              onClick={() =>
                window.open(createIssueUrl('bug_report'), '_blank')
              }
            >
              <Bug className="mr-2 h-4 w-4" />
              Report Bug
            </DropdownMenuItem>
            <DropdownMenuItem
              onClick={() =>
                window.open(createIssueUrl('feature_request'), '_blank')
              }
            >
              <Lightbulb className="mr-2 h-4 w-4" />
              Request Feature
            </DropdownMenuItem>
            <DropdownMenuItem
              onClick={() => window.open(createIssueUrl('question'), '_blank')}
            >
              <HelpCircle className="mr-2 h-4 w-4" />
              Ask Question
            </DropdownMenuItem>
            <DropdownMenuItem onClick={() => window.open(openIssuesUrl)}>
              <ListChecks className="mr-2 h-4 w-4" />
              View Open Issues
            </DropdownMenuItem>
          </DropdownMenuContent>
        </DropdownMenu>
      </TooltipProvider>
    </div>
  );
}
