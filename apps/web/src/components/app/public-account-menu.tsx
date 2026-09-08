'use client';

import { Link, useRouter } from '@/i18n/navigation';
import { useAuth } from '@game-guild/client/react';
import { Avatar, AvatarFallback, AvatarImage } from '@game-guild/ui/components/avatar';
import { Button } from '@game-guild/ui/components/button';
import { DropdownMenu, DropdownMenuContent, DropdownMenuItem, DropdownMenuSeparator, DropdownMenuTrigger } from '@game-guild/ui/components/dropdown-menu';
import { BriefcaseBusiness, ChevronsUpDown, LayoutDashboard, LogOut, Settings } from 'lucide-react';
import type { PublicWebsiteUser } from './public-website-nav';

export function PublicAccountMenu({ user }: { user: PublicWebsiteUser }) {
  const router = useRouter();
  const { signOut } = useAuth();

  return (
    <DropdownMenu>
      <DropdownMenuTrigger asChild>
        <Button
          variant="ghost"
          className="hidden h-11 max-w-72 justify-start gap-2 rounded-lg px-2 text-left text-foreground hover:bg-accent sm:inline-flex"
          aria-label={`Open ${user.name} account menu`}
        >
          <Avatar size="sm">
            <AvatarImage src={user.image ?? undefined} alt="" />
            <AvatarFallback className="bg-primary text-xs font-bold text-primary-foreground">
              {user.initials}
            </AvatarFallback>
          </Avatar>
          <span className="min-w-0 flex-1">
            <span className="block truncate text-sm font-semibold leading-tight">{user.name}</span>
            {user.email ? (
              <span className="block truncate text-xs leading-tight text-muted-foreground">{user.email}</span>
            ) : null}
          </span>
          <ChevronsUpDown className="size-4 shrink-0 text-muted-foreground" aria-hidden="true" />
        </Button>
      </DropdownMenuTrigger>
      <DropdownMenuContent align="end" className="w-56">
        <DropdownMenuItem asChild>
          <Link href="/workspace">
            <BriefcaseBusiness className="size-4" />
            My Workspace
          </Link>
        </DropdownMenuItem>
        {user.canManage && (
          <DropdownMenuItem asChild>
            <Link href="/dashboard">
              <LayoutDashboard className="size-4" />
              Dashboard
            </Link>
          </DropdownMenuItem>
        )}
        <DropdownMenuItem asChild>
          <Link href="/workspace/settings/account">
            <Settings className="size-4" />
            Account settings
          </Link>
        </DropdownMenuItem>
        <DropdownMenuSeparator />
        <DropdownMenuItem
          onClick={(event) => {
            event.preventDefault();
            void signOut({ redirect: false }).then(() => router.push('/sign-in'));
          }}
        >
          <LogOut className="size-4" />
          Sign out
        </DropdownMenuItem>
      </DropdownMenuContent>
    </DropdownMenu>
  );
}
