'use client';

import React from 'react';
import Link from 'next/link';

type Props = {
  page: number;
  pages: number;
};

export function CoursePagination({ page = 1, pages }: Readonly<Props>) {
  console.log('layout: ', page, ' pages: ', pages);

  return (
    <div className="p-[10px] align-top overflow-hidden text-center items-end">
      {page != 1 && (
        <span>
          <Link href={`/courses/${1}`} className="tracking-[-0.15 em]">
            &lt;&lt;
          </Link>
          &nbsp;&nbsp;
          <Link href={`/courses/page/${page - 1}`} className="">
            &lt;
          </Link>
        </span>
      )}

      {[...Array(3)].map((e, i) => (
        <span key={i}>
          {page - 3 + i > 0 && (
            <Link href={`/courses/page/${page - 3 + i}`} className="">
              {page - 3 + i}
            </Link>
          )}
          &nbsp;&nbsp;
        </span>
      ))}

      <span className="font-bold">{page}</span>

      {[...Array(3)].map((e, i) => (
        <span key={i}>
          &nbsp;&nbsp;
          {page + 1 + i <= pages && (
            <Link href={`/courses/page/${page + 1 + i}`} className="">
              {page + 1 + i}
            </Link>
          )}
        </span>
      ))}

      {page != pages && (
        <span>
          <Link href={`/courses/page/${page + 1}`} className="">
            &gt;
          </Link>
          &nbsp;&nbsp;
          <Link href={`/courses/page/${pages}`} className="tracking-[-0.15em]">
            &gt;&gt;
          </Link>
        </span>
      )}
    </div>
  );
}
