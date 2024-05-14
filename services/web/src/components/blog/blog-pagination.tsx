'use client';

import React from 'react';
import { useRouter } from 'next/navigation';
import { PostOrPage, Tag } from '@tryghost/content-api';
import Link from "next/link";

type Props = {
  page: number;
  pages: number;
};

export function BlogPagination( { page = 1, pages }: Readonly<Props>) {
  
  console.log("layout: ",page," pages: ",pages)

  return (
    <div className="p-[10px] align-top overflow-hidden text-center items-end" >
      {(page != 1) &&
        <span>
          <Link
            href={`/blog/${(1)}`}
            className='text-white tracking-[-0.15 em]'
          >
            &lt;&lt;
          </Link>
          &nbsp;&nbsp;
          <Link
            href={`/blog/page/${(page-1)}`}
            className='text-white'
          >
            &lt;
          </Link>
        </span>
      }

      {[...Array(3)].map((e,i) =>
        <span key={i}>
          {(page-3+i > 0) &&
            <Link
              href={`/blog/page/${(page-3+i)}`}
              className='text-white'
            >
              {(page-3+i)}
            </Link>
          }
          &nbsp;&nbsp;
        </span>
      )}

      <span className='font-bold'>{page}</span>

      {[...Array(3)].map((e,i) =>
        <span key={i}>
          &nbsp;&nbsp;
          {(page+1+i <= pages) &&
            <Link
            href={`/blog/page/${(page+1+i)}`}
            className='text-white'
          >
            { (page+1+i) }
          </Link>
          }
        </span>
      )}

      {(page != pages) &&
        <span>
          <Link
            href={`/blog/page/${(page+1)}`}
            className='text-white'
          >
            &gt;
          </Link>
          &nbsp;&nbsp;
          <Link
            href={`/blog/page/${(pages)}`}
            className='text-white tracking-[-0.15em]'
          >
            &gt;&gt;
          </Link>
        </span>
      }
    </div>
  );
}
