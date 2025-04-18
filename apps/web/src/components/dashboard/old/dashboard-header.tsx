import React from 'react';
import { Avatar, AvatarFallback, AvatarImage } from '@/components/ui/avatar';
import { Button } from '../../ui/button';
import { Bell, Search } from 'lucide-react';
import {
  DropdownMenu,
  DropdownMenuContent,
  DropdownMenuItem,
  DropdownMenuLabel,
  DropdownMenuSeparator,
  DropdownMenuTrigger,
} from '@/components/ui/dropdown-menu';
import { Dialog, DialogContent, DialogFooter, DialogTrigger } from '@/components/ui/dialog';

type Props = {
  children?: React.ReactNode;
};

export default function DashboardHeader({ children }: Readonly<Props>) {
  return (
    <header className="flex w-full">
      <div className="flex flex-grow h-20 items-center justify-between">
        <div className="hidden md:block">
          {/*    /!*TODO Convert user profile component here*!/*/}
          <div className="relative ml-auto flex-1 md:grow-0">
            <Dialog>
              <DialogTrigger asChild>
                <Button variant="ghost">
                  <div className="flex items-center justify-center gap-4">
                    <Search className="size-6 text-muted-foreground" />

                    <p className="text-muted-foreground">
                      Search{' '}
                      <kbd className="pointer-events-none inline-flex h-5 select-none items-center gap-1 rounded border bg-muted px-1.5 font-mono text-[10px] font-medium text-muted-foreground opacity-100">
                        <span className="text-base">⌘K</span>
                      </kbd>
                    </p>
                  </div>
                </Button>
              </DialogTrigger>
              <DialogContent className="sm:max-w-[425px]">
                {/*<DialogHeader>*/}
                {/*  <DialogTitle>*/}
                {/*    <div className="relative ml-auto flex-1 md:grow-0">*/}

                {/*      <Search className="absolute left-2.5 top-2.5 h-4 w-4 text-muted-foreground" />*/}
                {/*      <Input*/}
                {/*        type="search"*/}
                {/*        placeholder="Search..."*/}
                {/*        className="w-full rounded-lg bg-background pl-8 md:w-[200px] lg:w-[336px]"*/}
                {/*      />*/}
                {/*    </div>*/}
                {/*  </DialogTitle>*/}
                {/*  <DialogDescription>*/}
                {/*    Make changes to your profile here. Click save when you're done.*/}
                {/*  </DialogDescription>*/}
                {/*</DialogHeader>*/}
                {/*<div className="grid gap-4 py-4">*/}
                {/*  <div className="grid grid-cols-4 items-center gap-4">*/}
                {/*    <Label htmlFor="name" className="text-right">*/}
                {/*      Name*/}
                {/*    </Label>*/}
                {/*    <Input*/}
                {/*      id="name"*/}
                {/*      defaultValue="Pedro Duarte"*/}
                {/*      className="col-span-3"*/}
                {/*    />*/}
                {/*  </div>*/}
                {/*  <div className="grid grid-cols-4 items-center gap-4">*/}
                {/*    <Label htmlFor="username" className="text-right">*/}
                {/*      Username*/}
                {/*    </Label>*/}
                {/*    <Input*/}
                {/*      id="username"*/}
                {/*      defaultValue="@peduarte"*/}
                {/*      className="col-span-3"*/}
                {/*    />*/}
                {/*  </div>*/}
                {/*</div>*/}
                <DialogFooter>{/*<Button type="submit">Save changes</Button>*/}</DialogFooter>
              </DialogContent>
            </Dialog>
          </div>
        </div>
        <div className="hidden md:block">
          {/*  /!* <!-- User Profile --> *!/*/}
          <div className="ml-4 flex items-center md:ml-6 gap-2">
            <DropdownMenu>
              <DropdownMenuTrigger asChild>
                <Button variant="ghost" size="icon" className="overflow-hidden rounded-full">
                  <Bell className="size-6" />
                </Button>
              </DropdownMenuTrigger>
              <DropdownMenuContent align="end">
                <DropdownMenuLabel>My Account</DropdownMenuLabel>
                <DropdownMenuSeparator />
                <DropdownMenuItem>Settings</DropdownMenuItem>
                <DropdownMenuItem>Support</DropdownMenuItem>
                <DropdownMenuSeparator />
                <DropdownMenuItem>Logout</DropdownMenuItem>
              </DropdownMenuContent>
            </DropdownMenu>
            {/*    /!*TODO Convert user profile component here*!/*/}
            <DropdownMenu>
              <DropdownMenuTrigger asChild>
                <Avatar>
                  <AvatarImage src="https://github.com/shadcn.png" alt="@shadcn" />
                  <AvatarFallback>U</AvatarFallback>
                </Avatar>
              </DropdownMenuTrigger>
              <DropdownMenuContent align="end">
                <DropdownMenuLabel>My Account</DropdownMenuLabel>
                <DropdownMenuSeparator />
                <DropdownMenuItem>Settings</DropdownMenuItem>
                <DropdownMenuItem>Support</DropdownMenuItem>
                <DropdownMenuSeparator />
                <DropdownMenuItem>Logout</DropdownMenuItem>
              </DropdownMenuContent>
            </DropdownMenu>
          </div>
        </div>
        {/*  /!* <!-- Mobile Menu Toggle --> *!/*/}
        {/*  <div className="-mr-2 flex md:hidden">*/}
        {/*    /!*TODO Convert mobile menu toggle here*!/*/}
        {/*  </div>*/}
        {/*</div>*/}
      </div>
    </header>
  );
}
