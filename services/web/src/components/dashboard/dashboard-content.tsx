import React from 'react';
import { cva, VariantProps } from 'class-variance-authority';
import { cn } from '@/lib/utils';

const Variants = cva('flex flex-grow', {
  variants: {
    size: {
      compact: 'container',
      wide: 'px-8',
    },
  },
  defaultVariants: {
    size: 'compact',
  },
});

type Props = React.HTMLAttributes<HTMLDivElement> &
  VariantProps<typeof Variants> & {
  children?: React.ReactNode;
};

export default function DashboardContent({ children, size }: Readonly<Props>) {
  return (
    <div className={cn(Variants({ size }))}>
      <div className="flex flex-grow flex-col">{children}</div>
    </div>
  );
}
