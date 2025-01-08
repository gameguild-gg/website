import { Badge } from '@/components/ui/badge';
import { Card, CardContent } from '@/components/ui/card';
import { CircleDot, MessageCircle } from 'lucide-react';
import Link from 'next/link';

// This would typically come from an API call
const MOCK_ISSUES = [
  {
    id: 1,
    number: 72,
    title:
      'Services should run without a .env file shared by the others members',
    author: 'tolstenko',
    createdAt: '2024-01-08',
    comments: 2,
    labels: [],
    state: 'open',
  },
  {
    id: 2,
    number: 64,
    title: 'Error 404 page frontend redesign',
    author: 'tolstenko',
    createdAt: '2024-01-01',
    comments: 0,
    labels: ['design-&-marketing', 'front-end'],
    state: 'open',
  },
  {
    id: 3,
    number: 62,
    title: '404 error when we try to access Jobs page',
    author: 'mindtamorces',
    createdAt: '2024-01-01',
    comments: 1,
    labels: [],
    state: 'open',
  },
];

export function IssuesList() {
  return (
    <div className="space-y-4">
      {MOCK_ISSUES.map((issue) => (
        <Card key={issue.id}>
          <CardContent className="p-6">
            <div className="flex items-start justify-between gap-4">
              <div className="flex items-start gap-3">
                <CircleDot className="h-5 w-5 mt-1 text-green-500" />
                <div className="space-y-1">
                  <Link
                    href={`/issues/${issue.number}`}
                    className="text-lg font-semibold hover:text-primary hover:underline"
                  >
                    {issue.title}
                  </Link>
                  <div className="flex flex-wrap gap-2">
                    {issue.labels.map((label) => (
                      <Badge key={label} variant="outline">
                        {label}
                      </Badge>
                    ))}
                  </div>
                  <p className="text-sm text-muted-foreground">
                    #{issue.number} opened on {issue.createdAt} by{' '}
                    {issue.author}
                  </p>
                </div>
              </div>
              {issue.comments > 0 && (
                <div className="flex items-center gap-2 text-muted-foreground">
                  <MessageCircle className="h-4 w-4" />
                  <span className="text-sm">{issue.comments}</span>
                </div>
              )}
            </div>
          </CardContent>
        </Card>
      ))}
    </div>
  );
}
