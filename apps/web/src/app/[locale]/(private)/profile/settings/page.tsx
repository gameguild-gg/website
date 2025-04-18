'use client';

import { Button } from '@/components/ui/button';
import { Checkbox } from '@/components/ui/checkbox';
import { Input } from '@/components/ui/input';
import { Label } from '@/components/ui/label';
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from '@/components/ui/select';
import { Separator } from '@/components/ui/separator';
import { TextArea } from '@/components/ui/textArea';
import { Bold, Code, Heading2, Italic, Link, List, ListOrdered, Quote } from 'lucide-react';
import { useEffect, useState } from 'react';
import { Api, AuthApi } from '@game-guild/apiclient';
import { getSession } from 'next-auth/react';

export default function EditProfile() {
  const [user, setUser] = useState<Api.UserEntity>();
  const api = new AuthApi({
    basePath: process.env.NEXT_PUBLIC_API_URL,
  });

  //on mount
  useEffect(() => {
    getUser().then();
  }, []);

  // get user
  async function getUser(): Promise<void> {
    const session = await getSession();
    if (!session) {
      window.location.href = '/connect';
      return;
    }
    const response = await api.authControllerGetCurrentUser({
      headers: { Authorization: `Bearer ${session?.user?.accessToken}` },
    });
    if (response.status === 401) {
      window.location.href = '/disconnect';
      return;
    }
    if (response.status >= 400) {
      alert('Error fetching user data');
      return;
    }
    setUser(response.body as Api.UserEntity);
  }

  return (
    <div className="container mx-auto p-4">
      <div className="flex flex-col md:flex-row gap-8">
        <aside className="w-full md:w-64">
          <nav className="space-y-2">
            <Button variant="ghost" className="w-full justify-start">
              Profile
            </Button>
            <Button variant="ghost" className="w-full justify-start">
              Password
            </Button>
            <Button variant="ghost" className="w-full justify-start">
              Email addresses
            </Button>
            <Button variant="ghost" className="w-full justify-start">
              Two factor auth
            </Button>
            <Separator className="my-4" />
            <Button variant="ghost" className="w-full justify-start">
              Credit cards
            </Button>
            <Button variant="ghost" className="w-full justify-start">
              Billing address
            </Button>
            <Separator className="my-4" />
            <Button variant="ghost" className="w-full justify-start">
              Get started
            </Button>
            <Button variant="ghost" className="w-full justify-start">
              Support email
            </Button>
            <Button variant="ghost" className="w-full justify-start">
              Third-party analytics
            </Button>
            <Separator className="my-4" />
            <Button variant="ghost" className="w-full justify-start">
              Email notifications
            </Button>
            <Separator className="my-4" />
            <Button variant="ghost" className="w-full justify-start">
              Connected accounts
            </Button>
            <Button variant="ghost" className="w-full justify-start">
              Press access
            </Button>
            <Button variant="ghost" className="w-full justify-start">
              Privacy
            </Button>
            <Button variant="ghost" className="w-full justify-start">
              Data Export
            </Button>
            <Button variant="ghost" className="w-full justify-start">
              Delete Account
            </Button>
            <Separator className="my-4" />
            <Button variant="ghost" className="w-full justify-start">
              API keys
            </Button>
            <Button variant="ghost" className="w-full justify-start">
              OAuth applications
            </Button>
          </nav>
        </aside>
        <main className="flex-1">
          <h1 className="text-2xl font-bold mb-6">Profile</h1>
          <form className="space-y-6">
            <div className="space-y-2">
              <Label htmlFor="username">Username — Used to log into your account and for your page URL</Label>
              <div className="flex items-center gap-2">
                <Input
                  id="username"
                  defaultValue={user?.username}
                  className={user?.username ? '' : 'border-red-500'}
                  onChange={(e) =>
                    setUser({
                      ...user,
                      username: e.target.value,
                    } as Api.UserEntity)
                  }
                />
                <Button variant="secondary">Change my username/URL</Button>
              </div>
            </div>
            <div className="space-y-2">
              <Label htmlFor="url">URL — The public URL for your account</Label>
              <Input id="url" value="https://gameguild.gg/u/username" readOnly />
            </div>
            <div className="space-y-2">
              <Label>Profile Image — Shown next to your name when you take an action on the site (square dimensions)</Label>
              <Button>Upload Image</Button>
            </div>
            <div className="space-y-2">
              <Label htmlFor="display-name">Display name — Name to be shown in place of your username, leave blank to default to username</Label>
              <Input id="display-name" placeholder="optional" />
            </div>
            <div className="space-y-2">
              <Label htmlFor="website">Website — Optional URL to be shown on your profile page</Label>
              <Input id="website" placeholder="optional" />
            </div>
            <div className="space-y-2">
              <Label htmlFor="twitter">Twitter — Twitter account to show on your profile</Label>
              <Input id="twitter" placeholder="@username" />
            </div>
            <div className="space-y-2">
              <Label>Account type — How will you use your account</Label>
              <div className="space-y-2">
                <div className="flex items-center space-x-2">
                  <Checkbox id="playing" />
                  <label htmlFor="playing">Playing and downloading games</label>
                </div>
                <div className="flex items-center space-x-2">
                  <Checkbox id="developing" />
                  <label htmlFor="developing">Developing and uploading games</label>
                </div>
              </div>
            </div>
            <div className="space-y-2">
              <Label htmlFor="language">Language — Language of gameguild user interface</Label>
              <Select>
                <SelectTrigger>
                  <SelectValue placeholder="Default (Set to English)" />
                </SelectTrigger>
                <SelectContent>
                  <SelectItem value="en">English</SelectItem>
                  {/* Add more language options here */}
                </SelectContent>
              </Select>
              <p className="text-sm text-muted-foreground">
                gameguild is translated by members of our community. Some languages are only partially available. If you'd like to see gameguild in your
                language or help fix any mistakes you can contribute.{' '}
                <a href="#" className="text-primary hover:underline">
                  Learn more →
                </a>
              </p>
            </div>
            <div className="space-y-2">
              <Label>Content — How content on gameguild is shown to you</Label>
              <div className="space-y-2">
                <div className="flex items-center space-x-2">
                  <Checkbox id="adult-content" />
                  <label htmlFor="adult-content">Show content marked as adult in search & browse</label>
                </div>
                <div className="flex items-center space-x-2">
                  <Checkbox id="disable-gifs" />
                  <label htmlFor="disable-gifs">Disable animated gifs</label>
                </div>
                <div className="flex items-center space-x-2">
                  <Checkbox id="disable-autoload" />
                  <label htmlFor="disable-autoload">Disable auto-loading next page when browsing</label>
                </div>
                <div className="flex items-center space-x-2">
                  <Checkbox id="require-click" />
                  <label htmlFor="require-click">Require a click to run HTML5 game embeds</label>
                </div>
                <div className="flex items-center space-x-2">
                  <Checkbox id="dark-theme" />
                  <label htmlFor="dark-theme">Use a dark theme where available</label>
                </div>
                <div className="flex items-center space-x-2">
                  <Checkbox id="markdown" />
                  <label htmlFor="markdown">Prefer Markdown input where available</label>
                </div>
              </div>
            </div>
            <div className="space-y-2">
              <Label>Profile — The content of your profile page</Label>
              <div className="border rounded-md p-2">
                <div className="flex items-center gap-2 mb-2">
                  <Button size="sm" variant="outline">
                    <Code className="w-4 h-4" />
                  </Button>
                  <Button size="sm" variant="outline">
                    <Bold className="w-4 h-4" />
                  </Button>
                  <Button size="sm" variant="outline">
                    <Italic className="w-4 h-4" />
                  </Button>
                  <Button size="sm" variant="outline">
                    <Heading2 className="w-4 h-4" />
                  </Button>
                  <Button size="sm" variant="outline">
                    <List className="w-4 h-4" />
                  </Button>
                  <Button size="sm" variant="outline">
                    <ListOrdered className="w-4 h-4" />
                  </Button>
                  <Button size="sm" variant="outline">
                    <Link className="w-4 h-4" />
                  </Button>
                  <Button size="sm" variant="outline">
                    <Quote className="w-4 h-4" />
                  </Button>
                </div>
                <TextArea placeholder="Optional" className="min-h-[200px]" />
              </div>
            </div>
            <Button type="submit">Save</Button>
          </form>
        </main>
      </div>
    </div>
  );
}
