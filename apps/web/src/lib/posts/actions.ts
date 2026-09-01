"use server";

import { getToken } from "@/auth";
import { createServerClient } from "@game-guild/client";
import { revalidatePath } from "next/cache";
import { loadPosts, type PostsStream } from "./queries";

export async function loadPostsAction(
  stream: PostsStream,
  skip: number,
): Promise<{
  items: Awaited<ReturnType<typeof loadPosts>>;
  nextSkip: number | null;
}> {
  const items = await loadPosts(stream, skip);
  const nextSkip = items.length > 0 ? skip + items.length : null;
  return { items, nextSkip };
}

export interface CreatePostActionState {
  success: boolean;
  message: string | null;
}

export async function createPostAction(
  _previousState: CreatePostActionState,
  formData: FormData,
): Promise<CreatePostActionState> {
  const content = String(formData.get("content") ?? "").trim();
  const mediaUrlValue = String(formData.get("mediaUrl") ?? "").trim();

  if (!content) {
    return { success: false, message: "Write something before publishing." };
  }

  let mediaUrl: string | null = null;
  if (mediaUrlValue) {
    try {
      const parsed = new URL(mediaUrlValue);
      if (!["http:", "https:"].includes(parsed.protocol))
        throw new Error("Unsupported protocol");
      mediaUrl = parsed.toString();
    } catch {
      return { success: false, message: "Use a valid public media URL." };
    }
  }

  const client = createServerClient({
    baseUrl:
      process.env.API_URL ||
      process.env.NEXT_PUBLIC_API_URL ||
      "http://localhost:8080",
    auth: { getAccessToken: getToken },
  });
  const result = await client.request({
    method: "POST",
    path: "/api/v1/posts",
    requiresAuth: true,
    body: {
      content,
      visibility: "Public",
      mediaUrl,
      mediaType: mediaUrl ? "Image" : null,
    },
  });

  if (!result.ok) {
    return {
      success: false,
      message: result.error.message || "The post could not be published.",
    };
  }

  revalidatePath("/social");
  return { success: true, message: "Post published." };
}
