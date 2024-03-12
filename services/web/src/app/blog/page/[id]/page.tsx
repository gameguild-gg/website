import React from "react";
import { fetchPosts } from "@/actions/blog";
import { PostCard } from "@/components/blog/post-card";
import Link from "next/link";
import { BlogPagination } from '@/components/blog/blog-pagination';
type Props = {
  params: {
    id: number;
  };
};

async function Blog({ params: { id } }: Readonly<Props>) {
  const { posts, pagination } = await fetchPosts(id);

  return (
    <div>
      <div
        style={{
          textAlign: "center",
          color: "#fff",
          backgroundColor: "#101014",
          alignItems: "center",
          alignContent: "center",
          overflow: "hidden",
          width: "100%"
        }}
      >
        <div
          style={{
            maxWidth: "1200px",
            width: "100%",
            background: "#18181c" //#18181c
          }}
          className="grid grid-cols-1 md:grid-cols-3 mx-auto"
        >
          {posts &&
            posts.map((post: any) => <PostCard post={post} key={post.id} />)}
        </div>
        <div>
          <BlogPagination page={id} pages={pagination.pages} />
        </div>
      </div>
    </div>
  );
}

export default Blog;
