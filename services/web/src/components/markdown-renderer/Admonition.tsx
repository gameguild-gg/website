import React from 'react';
import {
  AlertCircle,
  AlertTriangle,
  BookOpen,
  Bug,
  CheckCircle,
  HelpCircle,
  Info,
  Quote,
  XCircle,
} from 'lucide-react';
import { cn } from '@/lib/utils';

type AdmonitionType =
  | 'note'
  | 'abstract'
  | 'info'
  | 'tip'
  | 'success'
  | 'question'
  | 'warning'
  | 'failure'
  | 'danger'
  | 'bug'
  | 'example'
  | 'quote';

interface AdmonitionProps {
  type: AdmonitionType;
  title?: string;
  children: React.ReactNode;
}

const iconMap: Record<AdmonitionType, React.ReactNode> = {
  note: <Info className="h-5 w-5" />,
  abstract: <BookOpen className="h-5 w-5" />,
  info: <Info className="h-5 w-5" />,
  tip: <CheckCircle className="h-5 w-5" />,
  success: <CheckCircle className="h-5 w-5" />,
  question: <HelpCircle className="h-5 w-5" />,
  warning: <AlertTriangle className="h-5 w-5" />,
  failure: <XCircle className="h-5 w-5" />,
  danger: <AlertCircle className="h-5 w-5" />,
  bug: <Bug className="h-5 w-5" />,
  example: <BookOpen className="h-5 w-5" />,
  quote: <Quote className="h-5 w-5" />,
};

const colorMap: Record<AdmonitionType, string> = {
  note: 'bg-blue-50 border-blue-500 text-blue-700',
  abstract: 'bg-purple-50 border-purple-500 text-purple-700',
  info: 'bg-cyan-50 border-cyan-500 text-cyan-700',
  tip: 'bg-green-50 border-green-500 text-green-700',
  success: 'bg-green-50 border-green-500 text-green-700',
  question: 'bg-yellow-50 border-yellow-500 text-yellow-700',
  warning: 'bg-amber-50 border-amber-500 text-amber-700',
  failure: 'bg-red-50 border-red-500 text-red-700',
  danger: 'bg-red-50 border-red-500 text-red-700',
  bug: 'bg-rose-50 border-rose-500 text-rose-700',
  example: 'bg-indigo-50 border-indigo-500 text-indigo-700',
  quote: 'bg-gray-50 border-gray-500 text-gray-700',
};

export function Admonition({ type, title, children }: AdmonitionProps) {
  return (
    <div
      className={cn(
        'admonition rounded-md border-l-4 p-4 mb-4',
        colorMap[type],
      )}
    >
      <div className="flex items-center mb-2">
        {iconMap[type]}
        <h5 className="ml-2 text-lg font-semibold">
          {title || type.charAt(0).toUpperCase() + type.slice(1)}
        </h5>
      </div>
      <div>{children}</div>
    </div>
  );
}
