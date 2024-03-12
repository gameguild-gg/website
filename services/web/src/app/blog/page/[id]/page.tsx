import React from "react";
import { fetchPosts } from "@/actions/blog";
import { PostCard } from "@/components/blog/post-card";
import Link from "next/link";

type Props = {
  params: {
    id: number;
  };
};

async function Blog({ params: { id } }: Readonly<Props>) {
  const { posts, pagination } = await fetchPosts(id);

  // TODO: Should move to a BlogPagination component.
  const pages = [];
  for (let i = 1; i <= pagination.pages; i++) {
    pages.push(
      <Link href={i == 1 ? '/blog' : `/blog/page/${i}`} key={i}>
        {i}
      </Link>,
    );
  }

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
          {/* TODO: Should be a BlogPagination Component!*/}
          {pages}
        </div>
      </div>
    </div>
  );
}

export default Blog;
