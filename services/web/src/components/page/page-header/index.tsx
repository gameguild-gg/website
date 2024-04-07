import React from "react";
import { PageHeaderRoot } from "@/components/page/page-header/page-header-root";
import { PageHeaderMenu } from "@/components/page/page-header/page-header-menu";
import { UserOutlined } from '@ant-design/icons';
import { Globe, Sun, Moon, SunMoon } from "lucide-react";

type Props = React.HTMLAttributes<HTMLDivElement> & {
  children?: React.ReactNode;
};

const PageHeader: React.FunctionComponent<Readonly<Props>> & {
  Menu: typeof PageHeaderMenu;
} = ({ children }: Readonly<Props>) => {
  return (
    <div className="overflow-hidden my-0 px-0 py-0 h-[70px] text-center text-white bg-neutral-900 w-full">
      {/*Left Corner of the Header*/}
      <div className="flex justify-between my-0 px-0 py-0 h-full w-full mx-auto">
        {/*Left Corner of the Header*/}
        <div className="flex my-0 py-0">
          <img
            src="assets/images/logo-text.png"
            className="w-[135px] h-46px my-auto mx-[10px]"
          />
          &nbsp;
          <a href="/about" className="flex my-0 font-semibold hover:bg-neutral-800 px-2 min-w-[80px] ">
            <span className="m-auto ">
              About
            </span>
          </a>
          <a href="/courses" className="flex my-0 font-semibold hover:bg-neutral-800 px-2 min-w-[80px] ">
            <span className="m-auto ">
              Courses
            </span>
          </a>
          <a href="/jobs" className="flex my-0 font-semibold hover:bg-neutral-800 px-2 min-w-[80px] ">
            <span className="m-auto ">
              Jobs
            </span>
          </a>
          <a href="/blog" className="flex my-0 font-semibold hover:bg-neutral-800 px-2 min-w-[80px] ">
            <span className="m-auto ">
              Blog
            </span>
          </a>
        </div>
        {/*Right Corner of the Header*/}
        <div className="flex justify-end m-0 p-0 items-center">
          <a href="/auth" className="flex my-0 font-semibold hover:bg-neutral-800 px-2 min-w-[80px] h-full">
            <span className="m-auto">
              Enter
            </span>
          </a>
          <a href="/auth">
            <button className="py-auto bg-white text-black border rounded-lg font-semibold p-1 hover:bg-neutral-900 hover:text-white">
              Register<br/>
            </button>
          </a>
          <div>
            <Globe className="w-[35px] mx-1 my-auto"/>
          </div>
        </div>
      </div>
    </div>
  );
};

PageHeader.Menu = PageHeaderMenu;

export { PageHeader };
