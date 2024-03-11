import React from "react";
import { fetchPosts } from "@/actions/blog";
import { PostCard } from "@/components/blog/post-card";
import { BlogPagination } from "@/components/blog/blog-pagination";

type Props = {
  searchParams: {
    page: any;
  };
};

async function Blog({ searchParams: { page = 1 } }: Readonly<Props>) {
  const { posts, pagination } = await fetchPosts(page);

  return (
    <div className='w-full min-h-screen overflow-hidden text-white bg-[#101014]'>
      <div className='mx-auto bg-[#18181c] w-full max-w-[1200px] min-h-screen'>
        <div className='grid grid-cols-1 md:grid-cols-3 mx-auto no-underline hover:no-underline'>
          {posts &&
            posts.map((post: any) => <PostCard post={post} key={post.id} />)}
        </div>
        <div>
          <BlogPagination page={parseInt(page)} pages={pagination.pages}/>
        </div>
      </div>
    </div>
  );
}

export default Blog;
