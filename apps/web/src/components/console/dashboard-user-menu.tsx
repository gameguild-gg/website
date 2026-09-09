'use client';

import { Link, useRouter } from '@/i18n/navigation';
import { useAuth } from '@game-guild/client/react';
import { Avatar, AvatarFallback, AvatarImage } from '@game-guild/ui/components/avatar';
import { Button } from '@game-guild/ui/components/button';
import {
  DropdownMenu,
  DropdownMenuContent,
  DropdownMenuItem,
  DropdownMenuSeparator,
  DropdownMenuTrigger,
} from '@game-guild/ui/components/dropdown-menu';
import { BriefcaseBusiness, ChevronsUpDown, LogOut, Settings } from 'lucide-react';
import * as React from 'react';

export interface DashboardUser {
  id: string;
  name: string;
  email: string;
  image?: string | null;
}

function getInitials(name: string, email: string) {
  const source = name.trim().length > 0 ? name : email.split('@')[0] ?? 'GG';
  const parts = source
    .split(/\s+/)
    .filter(Boolean)
    .slice(0, 2);

  return parts
    .map((part) => part[0])
    .join('')
    .toUpperCase();
}

export function DashboardUserMenu({ user }: { user: DashboardUser }) {
  const router = useRouter();
  const { signOut, isLoading } = useAuth();
  const [isSigningOut, setIsSigningOut] = React.useState(false);
  const disabled = isLoading || isSigningOut;

  const handleSignOut = React.useCallback(async () => {
    if (disabled) return;

    setIsSigningOut(true);

    try {
      await signOut({ redirect: false });
      router.push('/sign-in');
    } finally {
      setIsSigningOut(false);
    }
  }, [disabled, router, signOut]);

  return (
    <DropdownMenu>
      <DropdownMenuTrigger asChild>
        <Button
          variant="ghost"
          className="h-11 max-w-72 justify-start gap-2 rounded-lg px-2 text-left"
          aria-label={`Open ${user.name} account menu`}
        >
          <Avatar size="sm">
            {user.image ? <AvatarImage src={user.image} alt={user.name} /> : null}
            <AvatarFallback>{getInitials(user.name, user.email)}</AvatarFallback>
          </Avatar>
          <span className="hidden min-w-0 flex-1 sm:block">
            <span className="block truncate text-sm font-semibold leading-tight">{user.name}</span>
            <span className="block truncate text-xs leading-tight text-muted-foreground">{user.email}</span>
          </span>
          <ChevronsUpDown className="hidden size-4 shrink-0 text-muted-foreground sm:block" aria-hidden="true" />
        </Button>
      </DropdownMenuTrigger>
      <DropdownMenuContent align="end" className="w-64">
        <DropdownMenuItem asChild>
          <Link href="/workspace">
            <BriefcaseBusiness className="size-4" />
            My Workspace
          </Link>
        </DropdownMenuItem>
        <DropdownMenuItem asChild>
          <Link href="/workspace/settings/account">
            <Settings className="size-4" />
            Account settings
          </Link>
        </DropdownMenuItem>
        <DropdownMenuSeparator />
        <DropdownMenuItem
          disabled={disabled}
          onClick={(event) => {
            event.preventDefault();
            void handleSignOut();
          }}
        >
          <LogOut className="size-4" />
          {disabled ? 'Signing out...' : 'Sign out'}
        </DropdownMenuItem>
      </DropdownMenuContent>
    </DropdownMenu>
  );
}
