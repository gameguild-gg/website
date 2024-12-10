'use client';

import { useLayoutEffect, useState } from 'react';
import { useRouter } from 'next/navigation';
import { getSession } from 'next-auth/react';
import { Button } from '@/components/ui/button';
import {
  Card,
  CardContent,
  CardFooter,
  CardHeader,
  CardTitle,
} from '@/components/ui/card';
import { Input } from '@/components/ui/input';
import { Label } from '@/components/ui/label';
import { Textarea } from '@/components/ui/textarea';
import { Avatar, AvatarFallback, AvatarImage } from '@/components/ui/avatar';
import { useToast } from '@/components/ui/use-toast';
import { Api } from '@apiclient/models';
import { UsersApi } from '@apiclient/api';

export default function EditProfile() {
  const [user, setUser] = useState<Api.UserEntity>();

  const { toast } = useToast();
  const router = useRouter();

  const usersApi = new UsersApi({
    basePath: process.env.NEXT_PUBLIC_API_URL,
  });

  useLayoutEffect(() => {
    loadCurrentUser();
  }, []);

  const loadCurrentUser = async () => {
    const session: any = await getSession();
    if (!session) {
      router.push('/connect');
      return;
    }
    const response = await usersApi.userControllerGet({
      headers: { Authorization: `Bearer ${session.user.accessToken}` },
    });
    console.log('response: ', response);
    if (response.status == 200) {
      console.log('response.body: ', response.body);
      setUser(response.body);
    }
    console.log(response);
  };

  const handleAvatarChange = (e: React.ChangeEvent<HTMLInputElement>) => {
    const file = e.target.files?.[0];
    if (file) {
      const reader = new FileReader();
      reader.onloadend = () => {
        //setAvatarUrl(reader.result as string);
      };
      reader.readAsDataURL(file);
    }
  };

  const handleSave = async () => {
    const session: any = await getSession();
    if (!session) {
      router.push('/connect');
      return;
    }
    // const response = authApi.authControllerGetCurrentUser({});
    try {
      // // Simulating an API call
      // await new Promise((resolve, reject) => {
      //   setTimeout(() => {
      //     if (Math.random() > 0.5) {
      //       resolve('Success');
      //     } else {
      //       reject('Error');
      //     }
      //   }, 1000);
      // });

      // If the promise resolves, show success toast
      toast({
        title: 'Profile Saved',
        description: 'Your profile has been updated successfully.',
        duration: 3000,
      });
    } catch (error) {
      // If the promise rejects, show error toast
      toast({
        title: 'Error',
        description: 'Failed to save profile. Please try again.',
        variant: 'destructive',
        duration: 3000,
      });
    }
  };

  const handleReturn = () => {
    router.push('/feed');
  };

  return (
    <div className="container mx-auto p-4">
      <Card className="w-full max-w-2xl mx-auto">
        <CardHeader>
          <CardTitle className="text-2xl font-bold">Profile</CardTitle>
        </CardHeader>
        <CardContent className="space-y-4">
          <div className="flex flex-col items-center space-y-2">
            <Avatar className="w-32 h-32">
              <AvatarImage
                src={user?.profile?.picture || '/placeholder.svg'}
                alt="Profile picture"
              />
              <AvatarFallback>Avatar</AvatarFallback>
            </Avatar>
            <Label
              htmlFor="avatar-upload"
              className="cursor-pointer text-sm text-blue-500 hover:text-blue-600"
            >
              Change Avatar
            </Label>
            <Input
              id="avatar-upload"
              type="file"
              accept="image/*"
              className="hidden"
              onChange={handleAvatarChange}
            />
          </div>
          <div className="space-y-2">
            <Label htmlFor="username">Username</Label>
            <Input
              id="username"
              value={user?.username}
              onChange={(e) => {
                // setName(e.target.value);
              }}
              placeholder="Enter your username"
            />
          </div>
          <div className="space-y-2">
            <Label htmlFor="bio">Biography</Label>
            <Textarea
              id="bio"
              value={user?.profile?.bio}
              onChange={(e) => {
                // setBio(e.target.value)
              }}
              placeholder="Tell us about yourself"
              rows={4}
            />
          </div>
        </CardContent>
        <CardFooter className="flex justify-between">
          <Button onClick={handleReturn} variant="outline">
            Return
          </Button>
          <Button onClick={handleSave}>Save</Button>
        </CardFooter>
      </Card>
    </div>
  );
}
