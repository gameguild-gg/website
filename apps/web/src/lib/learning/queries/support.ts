import { getToken } from '@/auth';
import {
  createServerClient,
  GeneratedApi,
  type CommerceProductsSupportTicket,
  type CommerceProductsSupportTicketMessage,
  type LearningExperienceSocialServicesCourseDiscussion,
  type LearningExperienceSocialServicesDiscussionReply,
  type PagedResultOfGameGuildCommerceProductsSupportTicketDto,
} from '@game-guild/client';
import { cache } from 'react';
import { resolveCourseId } from './course';

// =============================================================================
// COURSE SUPPORT QUERIES
// =============================================================================
// Support tickets and discussions for enrolled students.
// =============================================================================

/**
 * Support ticket status
 */
export type TicketStatus = 'open' | 'in-progress' | 'waiting-on-student' | 'resolved' | 'closed';
export type TicketPriority = 'low' | 'normal' | 'high' | 'urgent';
export type TicketCategory = 'technical' | 'content' | 'billing' | 'access' | 'feedback' | 'other';

/**
 * Support ticket
 */
export interface SupportTicket {
  id: string;
  courseId: string;
  studentId: string;
  studentName: string;
  studentEmail: string;
  subject: string;
  status: TicketStatus;
  priority: TicketPriority;
  category: TicketCategory;
  messageCount: number;
  lastMessageAt: string;
  assignedTo?: {
    id: string;
    name: string;
  };
  createdAt: string;
  updatedAt: string;
}

export interface CourseSupportTickets {
  tickets: SupportTicket[];
  total: number;
  openCount: number;
  inProgressCount: number;
  resolvedCount: number;
}

/**
 * Ticket message
 */
export interface TicketMessage {
  id: string;
  ticketId: string;
  authorId: string;
  authorName: string;
  authorRole: 'student' | 'instructor' | 'support';
  content: string;
  attachments: Array<{
    id: string;
    name: string;
    url: string;
    size: number;
    type: string;
  }>;
  createdAt: string;
}

export interface SupportTicketDetail extends SupportTicket {
  messages: TicketMessage[];
  relatedContent?: {
    id: string;
    type: string;
    title: string;
  };
}

/**
 * Discussion thread (forum)
 */
export interface DiscussionThread {
  id: string;
  courseId: string;
  contentItemId?: string;     // Optional link to specific content
  authorId: string;
  authorName: string;
  authorAvatar?: string;
  title: string;
  content: string;
  pinned: boolean;
  locked: boolean;
  replyCount: number;
  viewCount: number;
  lastReplyAt: string | null;
  tags: string[];
  createdAt: string;
  updatedAt: string;
}

export interface CourseDiscussions {
  threads: DiscussionThread[];
  total: number;
  pinnedCount: number;
}

/**
 * Discussion reply
 */
export interface DiscussionReply {
  id: string;
  threadId: string;
  parentId?: string;          // For nested replies
  authorId: string;
  authorName: string;
  authorAvatar?: string;
  authorRole: 'student' | 'instructor' | 'ta';
  content: string;
  upvotes: number;
  isAnswer: boolean;          // Marked as accepted answer
  createdAt: string;
  updatedAt: string;
}

export interface DiscussionThreadDetail extends DiscussionThread {
  replies: DiscussionReply[];
}

function createSupportModules() {
  const apiUrl = process.env.API_URL || process.env.NEXT_PUBLIC_API_URL || 'http://localhost:8080';
  const client = createServerClient({
    baseUrl: apiUrl,
    auth: { getAccessToken: () => getToken() },
  });

  return {
    tickets: new GeneratedApi.LearningCoursesSupportticketsModule(client),
    discussions: new GeneratedApi.LearningExperienceSocialDiscussionsModule(client),
    replies: new GeneratedApi.LearningExperienceSocialRepliesModule(client),
  };
}

function mapTicketStatus(status: string | null | undefined): TicketStatus {
  if (status === 'InProgress') return 'in-progress';
  if (status === 'Resolved') return 'resolved';
  if (status === 'Closed' || status === 'Cancelled') return 'closed';
  return 'open';
}

function mapTicketPriority(priority: string | null | undefined): TicketPriority {
  const normalized = priority?.toLowerCase();
  return normalized === 'low' || normalized === 'high' || normalized === 'urgent' ? normalized : 'normal';
}

function mapTicketCategory(category: string | null | undefined): TicketCategory {
  const normalized = category?.toLowerCase();
  return normalized === 'technical' || normalized === 'content' || normalized === 'billing' || normalized === 'access' || normalized === 'feedback'
    ? normalized
    : 'other';
}

function mapSupportTicket(dto: CommerceProductsSupportTicket): SupportTicket {
  const openedAt = dto.openedAt ?? new Date().toISOString();
  const lastMessageAt = dto.lastMessageAt ?? openedAt;

  return {
    id: dto.id ?? '',
    courseId: dto.customerId ?? '',
    studentId: dto.reporterUserId ?? '',
    studentName: dto.reporterName ?? 'Student',
    studentEmail: dto.reporterEmail ?? '',
    subject: dto.subject ?? '',
    status: mapTicketStatus(dto.status),
    priority: mapTicketPriority(dto.priority),
    category: mapTicketCategory(dto.category),
    messageCount: dto.messageCount ?? 0,
    lastMessageAt,
    assignedTo: dto.assignedToUserId && dto.assignedToName
      ? { id: dto.assignedToUserId, name: dto.assignedToName }
      : undefined,
    createdAt: openedAt,
    updatedAt: lastMessageAt,
  };
}

// =============================================================================
// FETCH FUNCTIONS
// =============================================================================

/**
 * Fetch course support tickets.
 * Cache: revalidate 30s (highly volatile)
 */
export const getCourseSupportTickets = cache(async (courseId: string): Promise<CourseSupportTickets> => {
  const resolvedCourseId = await resolveCourseId(courseId);
  const result = await createSupportModules().tickets.getCoursesSupportTickets(resolvedCourseId, {
    skip: 0,
    take: 100,
  });
  const response: PagedResultOfGameGuildCommerceProductsSupportTicketDto | undefined = result.ok
    ? result.data
    : undefined;
  const tickets = (response?.items ?? []).map(mapSupportTicket);

  return {
    tickets,
    total: response?.totalCount ?? tickets.length,
    openCount: tickets.filter((ticket) => ticket.status === 'open').length,
    inProgressCount: tickets.filter((ticket) => ticket.status === 'in-progress').length,
    resolvedCount: tickets.filter((ticket) => ticket.status === 'resolved' || ticket.status === 'closed').length,
  };
});

/**
 * Fetch single ticket detail with messages.
 * Cache: revalidate 30s
 */
export const getSupportTicket = cache(async (courseId: string, ticketId: string): Promise<SupportTicketDetail | null> => {
  const resolvedCourseId = await resolveCourseId(courseId);
  const result = await createSupportModules().tickets.getCoursesSupportTickets1(resolvedCourseId, ticketId);
  if (!result.ok) return null;

  const dto = result.data;
  return {
    ...mapSupportTicket(dto),
    messages: (dto.messages ?? []).map((message: CommerceProductsSupportTicketMessage) => ({
      id: message.id ?? '',
      ticketId: message.ticketId ?? ticketId,
      authorId: message.authorUserId ?? '',
      authorName: message.authorName ?? 'Support',
      authorRole: message.authorType === 'Customer' ? 'student' : message.authorType === 'Agent' ? 'instructor' : 'support',
      content: message.body ?? '',
      attachments: [],
      createdAt: message.createdAt ?? new Date().toISOString(),
    })),
  };
});

function mapDiscussion(dto: LearningExperienceSocialServicesCourseDiscussion): DiscussionThread {
  const createdAt = dto.createdAt ?? new Date().toISOString();
  const authorId = dto.authorId ?? '';

  return {
    id: dto.id ?? '',
    courseId: dto.courseId ?? '',
    contentItemId: dto.contentId ?? undefined,
    authorId,
    authorName: authorId ? `Student ${authorId.slice(0, 8)}` : 'Student',
    title: dto.title ?? '',
    content: dto.content ?? '',
    pinned: dto.isPinned ?? false,
    locked: dto.isResolved ?? false,
    replyCount: dto.replyCount ?? 0,
    viewCount: dto.viewCount ?? 0,
    lastReplyAt: dto.lastActivityAt ?? null,
    tags: [],
    createdAt,
    updatedAt: dto.lastActivityAt ?? createdAt,
  };
}


/**
 * Fetch course discussions (conditional: hasDiscussions).
 * Cache: revalidate 60s
 */
export const getCourseDiscussions = cache(async (courseId: string): Promise<CourseDiscussions> => {
  const resolvedCourseId = await resolveCourseId(courseId);
  const result = await createSupportModules().discussions.getApiSocialCoursesDiscussions(resolvedCourseId, {
    skip: 0,
    take: 100,
    pinnedFirst: true,
  });
  const threads = (result.ok ? result.data : []).map(mapDiscussion);

  return { threads, total: threads.length, pinnedCount: threads.filter((thread) => thread.pinned).length };
});

/**
 * Fetch single discussion thread with replies.
 * Cache: revalidate 60s
 */
export const getDiscussionThread = cache(async (threadId: string): Promise<DiscussionThreadDetail | null> => {
  const modules = createSupportModules();
  const [discussionResult, repliesResult] = await Promise.all([
    modules.discussions.getApiSocialDiscussions(threadId),
    modules.replies.getApiSocialDiscussionsReplies(threadId, { skip: 0, take: 100 }),
  ]);

  if (!discussionResult.ok) return null;

  const thread = mapDiscussion(discussionResult.data);
  const replies = repliesResult.ok ? repliesResult.data : [];
  return {
    ...thread,
    replies: replies.map((reply: LearningExperienceSocialServicesDiscussionReply) => {
      const createdAt = reply.createdAt ?? new Date().toISOString();
      const authorId = reply.authorId ?? '';

      return {
        id: reply.id ?? '',
        threadId: reply.discussionId ?? threadId,
        parentId: reply.parentReplyId ?? undefined,
        authorId,
        authorName: authorId ? `Member ${authorId.slice(0, 8)}` : 'Member',
        authorRole: 'student',
        content: reply.content ?? '',
        upvotes: reply.upvoteCount ?? 0,
        isAnswer: reply.isAcceptedAnswer ?? false,
        createdAt,
        updatedAt: createdAt,
      };
    }),
  };
});
