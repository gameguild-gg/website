'use client';

import { Link, useRouter } from '@/i18n/navigation';
import { useAuth } from '@game-guild/client/react';
import { Avatar, AvatarFallback, AvatarImage } from '@game-guild/ui/components/avatar';
import { Button } from '@game-guild/ui/components/button';
import { DropdownMenu, DropdownMenuContent, DropdownMenuItem, DropdownMenuLabel, DropdownMenuSeparator, DropdownMenuTrigger } from '@game-guild/ui/components/dropdown-menu';
import { BriefcaseBusiness, LayoutDashboard, LogOut, Settings } from 'lucide-react';
import type { PublicWebsiteUser } from './public-website-nav';

export function PublicAccountMenu({ user }: { user: PublicWebsiteUser }) {
  const router = useRouter();
  const { signOut } = useAuth();

  return (
    <DropdownMenu>
      <DropdownMenuTrigger asChild>
        <Button
          variant="ghost"
          size="icon"
          className="hidden size-9 rounded-full text-foreground hover:bg-accent sm:inline-flex"
          aria-label={`Open ${user.name} account menu`}
        >
          <Avatar size="sm">
            <AvatarImage src={user.image ?? undefined} alt="" />
            <AvatarFallback className="bg-primary text-xs font-bold text-primary-foreground">
              {user.initials}
            </AvatarFallback>
          </Avatar>
        </Button>
      </DropdownMenuTrigger>
      <DropdownMenuContent align="end" className="w-56">
        <DropdownMenuLabel className="font-normal">
          <p className="truncate text-sm font-medium">{user.name}</p>
          {user.email && <p className="truncate text-xs text-muted-foreground">{user.email}</p>}
        </DropdownMenuLabel>
        <DropdownMenuSeparator />
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
