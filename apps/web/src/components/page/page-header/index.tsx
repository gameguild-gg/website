import React from 'react';
import { PageHeaderMenu } from '@/components/page/page-header/page-header-menu';
import { Bell, Globe, Search, ShoppingCart } from 'lucide-react';
import { Input } from '@/components/ui/input';
import { Button } from '@/components/ui/button';

type Props = React.HTMLAttributes<HTMLDivElement> & {
  children?: React.ReactNode;
};

const PageHeader: React.FunctionComponent<Readonly<Props>> & {
  Menu: typeof PageHeaderMenu;
} = ({ children }: Readonly<Props>) => {
  // todo: if the user is logged or not, between a button to connect or the user icon with the username

  /*
          <Button variant="ghost" className="flex items-center space-x-2">
            <span>username</span>
            <ChevronDown size={16} />
          </Button>
   */

  return (
    <div className="overflow-hidden my-0 px-0 py-0 h-[70px] text-center text-white bg-neutral-900 w-full">
      {/*Left Corner of the Header*/}
      <div className="flex justify-between my-0 px-0 py-0 h-full w-full mx-auto">
        {/*Left Corner of the Header*/}
        <div className="flex my-0 py-0">
          <a href="/" className="flex my-0 py-0">
            <img
              src="/assets/images/logo-text.png"
              className="w-[135px] h-46px my-auto mx-[10px]"
            />
          </a>
          <a
            href="/feed"
            className="flex my-0 font-semibold hover:bg-neutral-800 px-2 min-w-[80px] hover:text-yellow-400"
          >
            <span className="m-auto ">Feed</span>
          </a>
          <a
            href="/games"
            className="flex my-0 font-semibold hover:bg-neutral-800 px-2 min-w-[80px] hover:text-yellow-400"
          >
            <span className="m-auto ">Games</span>
          </a>
          <a
            href="/competition"
            className="flex my-0 font-semibold hover:bg-neutral-800 px-2 min-w-[80px] hover:text-yellow-400"
          >
            <span className="m-auto ">Competition</span>
          </a>
          <a
            href="/courses"
            className="flex my-0 font-semibold hover:bg-neutral-800 px-2 min-w-[80px] hover:text-blue-400"
          >
            <span className="m-auto ">Courses</span>
          </a>
          <a
            href="/gtl"
            className="flex my-0 font-semibold hover:bg-neutral-800 px-2 min-w-[80px] hover:text-green-400"
          >
            <span className="m-auto ">GTL</span>
          </a>
          <a
            href="/jams"
            className="flex my-0 font-semibold hover:bg-neutral-800 px-2 min-w-[80px] hover:text-purple-400"
          >
            <span className="m-auto ">Jams</span>
          </a>
          <a
            href="/blog"
            className="flex my-0 font-semibold hover:bg-neutral-800 px-2 min-w-[80px] hover:text-red-400"
          >
            <span className="m-auto ">Blog</span>
          </a>
          <a
            href="/Community"
            className="flex my-0 font-semibold hover:bg-neutral-800 px-2 min-w-[80px] hover:text-red-400"
          >
            <span className="m-auto ">Community</span>
          </a>
        </div>
        {/*Right Corner of the Header*/}
        <div className="flex items-center space-x-4">
          <div className="relative">
            <Input
              type="search"
              placeholder="Search for games or creators"
              className="pl-10 pr-4 py-2 rounded-full bg-gray-100"
            />
            <Search
              className="absolute left-3 top-1/2 transform -translate-y-1/2 text-gray-400"
              size={20}
            />
          </div>
          <Button variant="ghost" size="icon">
            <Bell size={20} />
          </Button>
          <Button variant="ghost" size="icon">
            <ShoppingCart size={20} />
          </Button>

          <a href="/connect">
            <button
              className="py-auto bg-white text-black border rounded-lg font-semibold p-1 hover:bg-neutral-900 hover:text-white">
              Connect
              <br />
            </button>
          </a>
          <div>
            <Globe className="w-[35px] mx-1 my-auto" />
          </div>
        </div>
      </div>
    </div>
  );
};

PageHeader.Menu = PageHeaderMenu;

export { PageHeader };
