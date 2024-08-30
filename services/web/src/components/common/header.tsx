import React from 'react';
import {Input,} from '../ui/input';
import {Button} from '../ui/button';
import Link from "next/link";

export default async function Header() {
  return (
    <div className="bg-[#f2f2f2]">
      <div className="max-w-screen-xl mx-auto px-4">
        <div className="flex justify-between items-center border-b py-4">
          <div className="flex items-center space-x-4">
            <img
              src="/placeholder.svg"
              alt="Logo"
              className="h-10 w-10"
              width="40"
              height="40"
              style={{aspectRatio: "40/40", objectFit: "cover"}}
            />
            <span className="text-xl font-bold">Game Developer</span>
          </div>
          <div className="flex space-x-6">
            <Link href="#" className="text-sm" prefetch={false}>
              Game Market Research
            </Link>
            <Link href="#" className="text-sm" prefetch={false}>
              GDC Vault
            </Link>
            <Link href="#" className="text-sm" prefetch={false}>
              Advertise With Game Developer
            </Link>
          </div>
        </div>
        <div className="flex justify-between items-center py-4">
          <div className="flex space-x-4">
            <Button className="bg-black text-white">News</Button>
            {/*<Select>*/}
            {/*  <SelectTrigger id="trending">*/}
            {/*    <SelectValue placeholder="Trending"/>*/}
            {/*  </SelectTrigger>*/}
            {/*  <SelectContent position="popper">*/}
            {/*    <SelectItem value="trending-1">Trending 1</SelectItem>*/}
            {/*    <SelectItem value="trending-2">Trending 2</SelectItem>*/}
            {/*  </SelectContent>*/}
            {/*</Select>*/}
            <Link href="#" className="text-sm" prefetch={false}>
              GDC 2024
            </Link>
            <Link href="#" className="text-sm" prefetch={false}>
              Game Design
            </Link>
            <Link href="#" className="text-sm" prefetch={false}>
              Marketing Tips
            </Link>
            <Link href="#" className="text-sm" prefetch={false}>
              Programming
            </Link>
            {/*<Select>*/}
            {/*  <SelectTrigger id="topics">*/}
            {/*    <SelectValue placeholder="Topics"/>*/}
            {/*  </SelectTrigger>*/}
            {/*  <SelectContent position="popper">*/}
            {/*    <SelectItem value="topic-1">Topic 1</SelectItem>*/}
            {/*    <SelectItem value="topic-2">Topic 2</SelectItem>*/}
            {/*  </SelectContent>*/}
            {/*</Select>*/}
            {/*<Select>*/}
            {/*  <SelectTrigger id="blogs">*/}
            {/*    <SelectValue placeholder="Blogs"/>*/}
            {/*  </SelectTrigger>*/}
            {/*  <SelectContent position="popper">*/}
            {/*    <SelectItem value="blog-1">Blog 1</SelectItem>*/}
            {/*    <SelectItem value="blog-2">Blog 2</SelectItem>*/}
            {/*  </SelectContent>*/}
            {/*</Select>*/}
            {/*<Select>*/}
            {/*  <SelectTrigger id="jobs">*/}
            {/*    <SelectValue placeholder="Jobs"/>*/}
            {/*  </SelectTrigger>*/}
            {/*  <SelectContent position="popper">*/}
            {/*    <SelectItem value="job-1">Job 1</SelectItem>*/}
            {/*    <SelectItem value="job-2">Job 2</SelectItem>*/}
            {/*  </SelectContent>*/}
            {/*</Select>*/}
          </div>
          <div className="flex items-center space-x-4">
            <Input type="search" placeholder="Search" className="border px-2 py-1"/>
            <Button variant="secondary">Stay Updated</Button>
          </div>
        </div>
      </div>
    </div>
  );
}