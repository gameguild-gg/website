import React from "react";
import { PageHeaderRoot } from "@/components/page/page-header/page-header-root";
import { PageHeaderMenu } from "@/components/page/page-header/page-header-menu";
import { UserOutlined } from '@ant-design/icons';

type Props = React.HTMLAttributes<HTMLDivElement> & {
  children?: React.ReactNode;
};

const PageHeader: React.FunctionComponent<Readonly<Props>> & {
  Menu: typeof PageHeaderMenu;
} = ({ children }: Readonly<Props>) => {
  return (
    <div
      style={{
        textAlign: 'center',
        color: '#fff',
        height: 64,
        paddingInline: 48,
        lineHeight: '60px',
        padding: '2px',
        backgroundColor: '#18181c',
      }}
      className="overflow-hidden"
    >
      <div
        style={{
          textAlign: 'center',
          width: '100%',
          display: 'inline-flex',
          alignItems: 'center',
        }}
        className="grid grid-cols-2 justify-between"
      >
        <div
          style={{
            display: 'flex',
            minWidth: '150px',
            alignContent: 'center',
          }}
        >
          <img
            style={{ width: 135, height: 46, margin: "7px" }}
            src="assets/images/logo-text.png"
          />
          &nbsp;
          {/*<a href="/">*/}
          {/*  <span*/}
          {/*    style={{*/}
          {/*      paddingLeft: '15px',*/}
          {/*      paddingRight: '15px',*/}
          {/*      color: '#ffffff',*/}
          {/*    }}*/}
          {/*  >*/}
          {/*    About*/}
          {/*  </span>*/}
          {/*</a>*/}
          {/*<a href="/">*/}
          {/*  <span*/}
          {/*    style={{*/}
          {/*      paddingLeft: '15px',*/}
          {/*      paddingRight: '15px',*/}
          {/*      color: '#ffffff',*/}
          {/*    }}*/}
          {/*  >*/}
          {/*    Courses*/}
          {/*  </span>*/}
          {/*</a>*/}
          {/*<a href="/">*/}
          {/*  <span*/}
          {/*    style={{*/}
          {/*      paddingLeft: '15px',*/}
          {/*      paddingRight: '15px',*/}
          {/*      color: '#ffffff',*/}
          {/*    }}*/}
          {/*  >*/}
          {/*    Jobs*/}
          {/*  </span>*/}
          {/*</a>*/}
          {/*<a href="/">*/}
          {/*  <span*/}
          {/*    style={{*/}
          {/*      paddingLeft: '15px',*/}
          {/*      paddingRight: '15px',*/}
          {/*      color: '#ffffff',*/}
          {/*    }}*/}
          {/*  >*/}
          {/*    Blog*/}
          {/*  </span>*/}
          {/*</a>*/}
          <a href="/competition">
            <span
              style={{
                paddingLeft: '15px',
                paddingRight: '15px',
                color: '#ffffff',
              }}
            >
              Competition
            </span>
          </a>
        </div>
        <div style={{ display: 'flex' }}>
          <a href="/login">
            <span
              style={{
                padding: '15px',
                paddingTop: '10px',
                paddingBottom: '10px',
                color: '#000000',
                borderRadius: '30px',
                backgroundColor: '#ffffff',
              }}
            >
              <UserOutlined /> Login
            </span>
          </a>
          <img
            width={25}
            src="assets/images/language.svg"
            style={{ margin: '7px'}}
          />
        </div>
      </div>
    </div>
  );
};

PageHeader.Menu = PageHeaderMenu;

export { PageHeader };
