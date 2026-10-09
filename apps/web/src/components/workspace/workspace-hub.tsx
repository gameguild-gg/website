import { Link } from '@/i18n/navigation';
import { listMyBlogPosts, type MyBlogPostRow } from '@/lib/blogs/queries';
import { getWorkspaceMyTeamInvitations, getWorkspaceProjects, getWorkspaceTeams } from '@/lib/workspaces';
import { Badge } from '@game-guild/ui/components/badge';
import { buttonVariants } from '@game-guild/ui/components/button-variants';
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@game-guild/ui/components/card';
import { CheckSquare2, FileText, FolderKanban, Mail, Plus, Users } from 'lucide-react';
import React from 'react';

function projectStatusLabel(value: string | number) {
  const labels: Record<string, string> = { Draft: 'Draft', Review: 'In review', Published: 'Published', Archived: 'Archived' };
  return labels[String(value)] ?? String(value).replace(/([a-z])([A-Z])/g, '$1 $2');
}

function postStatusLabel(value: string | number) {
  const labels: Record<string, string> = { Draft: 'Draft', Published: 'Published' };
  return labels[String(value)] ?? String(value).replace(/([a-z])([A-Z])/g, '$1 $2');
}

function postEditedAt(post: { publishedAt: string | null; updatedAt: string }) {
  const at = post.publishedAt ?? post.updatedAt;
  return new Date(at).toLocaleDateString(undefined, { year: 'numeric', month: 'short', day: 'numeric' });
}

async function getWorkspaceBlogPosts(): Promise<{ available: boolean; posts: MyBlogPostRow[] }> {
  try {
    return { available: true, posts: await listMyBlogPosts() };
  } catch {
    return { available: false, posts: [] };
  }
}

export async function WorkspaceHub(): Promise<React.JSX.Element> {
  const [teams, projects, invitations, blogResult] = await Promise.all([
    getWorkspaceTeams(),
    getWorkspaceProjects(),
    getWorkspaceMyTeamInvitations(),
    getWorkspaceBlogPosts(),
  ]);
  const { available: blogPostsAvailable, posts: blogPosts } = blogResult;

  return (
    <div className="space-y-6">
      <header className="flex flex-wrap items-end justify-between gap-4">
        <div>
          <Badge variant="outline">Workspace</Badge>
          <h1 className="mt-2 text-3xl font-bold tracking-tight">Your teams and projects</h1>
          <p className="mt-1 max-w-2xl text-muted-foreground">
            Build with your teams, manage project work, and submit eligible versions to community events.
          </p>
        </div>
        <div className="flex gap-2">
          <Link href="/workspace/teams/new" className={buttonVariants({ variant: 'outline' })}><Plus className="size-4" />Team</Link>
          <Link href="/workspace/projects/new" className={buttonVariants()}><Plus className="size-4" />Project</Link>
          <Link href="/blog/new" className={buttonVariants({ variant: 'outline' })}><Plus className="size-4" />Post</Link>
        </div>
      </header>

      <div className="grid gap-4 sm:grid-cols-3">
        <Metric icon={<Users className="size-4" />} label="Teams" value={teams.length} />
        <Metric icon={<FolderKanban className="size-4" />} label="Projects" value={projects.length} />
        <Metric icon={<Mail className="size-4" />} label="Invitations" value={invitations.length} />
        <Metric
          icon={<FileText className="size-4" />}
          label="Blog posts"
          value={blogPostsAvailable ? blogPosts.length : 'Unavailable'}
        />
      </div>

      <div className="grid gap-6 lg:grid-cols-2">
        <Card>
          <CardHeader className="flex-row items-center justify-between space-y-0">
            <div><CardTitle>Recent teams</CardTitle><CardDescription>Active team memberships.</CardDescription></div>
            <Link href="/workspace/teams" className={buttonVariants({ size: 'sm', variant: 'ghost' })}>All teams</Link>
          </CardHeader>
          <CardContent className="space-y-2">
            {teams.slice(0, 5).map((team) => (
              <Link key={team.id} href={`/workspace/teams/${team.slug}`} className="flex items-center justify-between rounded-lg border p-3 transition hover:bg-muted/50">
                <span className="font-medium">{team.name}</span>
                <Badge variant="secondary">Team</Badge>
              </Link>
            ))}
            {teams.length === 0 && <Empty message="Create a team to collaborate on projects." />}
          </CardContent>
        </Card>
        <Card>
          <CardHeader className="flex-row items-center justify-between space-y-0">
            <div><CardTitle>Recent projects</CardTitle><CardDescription>Projects you can access.</CardDescription></div>
            <Link href="/workspace/projects" className={buttonVariants({ size: 'sm', variant: 'ghost' })}>All projects</Link>
          </CardHeader>
          <CardContent className="space-y-2">
            {projects.slice(0, 5).map((project) => (
              <Link key={project.id} href={`/workspace/projects/${project.slug}`} className="flex items-center justify-between rounded-lg border p-3 transition hover:bg-muted/50">
                <span className="font-medium">{project.title}</span>
                <Badge variant="secondary">{projectStatusLabel(project.status)}</Badge>
              </Link>
            ))}
            {projects.length === 0 && <Empty message="Create a project or join a team project." />}
          </CardContent>
        </Card>
      </div>

      <Card>
        <CardHeader className="flex-row items-center justify-between space-y-0">
          <div>
            <CardTitle className="flex items-center gap-2"><FileText className="size-4" />Recent blog posts</CardTitle>
            <CardDescription>Drafts and published articles under your handle.</CardDescription>
          </div>
          <Link href="/blog/new" className={buttonVariants({ size: 'sm', variant: 'ghost' })}>New post</Link>
        </CardHeader>
        <CardContent>
          <div className="max-h-72 space-y-2 overflow-y-auto">
            {blogPosts.map((post) => {
              const href = post.primaryAuthorHandle && post.slug ? `/blogs/${post.primaryAuthorHandle}/${post.slug}/edit` : '/blogs';
              return (
                <Link key={post.id} href={href} className="flex items-center justify-between gap-4 rounded-lg border p-3 transition hover:bg-muted/50">
                  <span className="min-w-0 truncate font-medium">{post.title}</span>
                  <span className="flex shrink-0 items-center gap-3 text-sm text-muted-foreground">
                    {postEditedAt(post)}
                    <Badge variant="secondary">{postStatusLabel(post.status)}</Badge>
                  </span>
                </Link>
              );
            })}
          </div>
          {!blogPostsAvailable && <Empty message="Blog posts are temporarily unavailable." />}
          {blogPostsAvailable && blogPosts.length === 0 && (
            <Empty message="No posts yet.">
              <Link href="/blog/new" className={buttonVariants({ variant: 'outline' })}><Plus className="size-4" />New post</Link>
            </Empty>
          )}
        </CardContent>
      </Card>

      <Card>
        <CardHeader>
          <CardTitle className="flex items-center gap-2"><CheckSquare2 className="size-4" />My work</CardTitle>
          <CardDescription>Tasks live inside the project that owns them.</CardDescription>
        </CardHeader>
        <CardContent><Link href="/workspace/work" className={buttonVariants({ variant: 'outline' })}>Open assigned work</Link></CardContent>
      </Card>
    </div>
  );
}

function Metric({ icon, label, value }: { icon: React.ReactNode; label: string; value: number | string }) {
  return <Card><CardHeader className="flex-row items-center justify-between space-y-0 pb-2"><CardTitle className="text-sm font-medium">{label}</CardTitle>{icon}</CardHeader><CardContent><p className={typeof value === 'number' ? 'text-2xl font-semibold' : 'text-sm font-medium text-muted-foreground'}>{value}</p></CardContent></Card>;
}

function Empty({ message, children }: { message: string; children?: React.ReactNode }) {
  return (
    <div className="py-6 text-center">
      <p className="text-sm text-muted-foreground">{message}</p>
      {children && <div className="mt-3 flex justify-center">{children}</div>}
    </div>
  );
}
