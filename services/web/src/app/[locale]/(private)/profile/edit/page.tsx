'use client';

import React, { useLayoutEffect, useState } from 'react';
import { useRouter } from 'next/navigation';
import { getSession } from 'next-auth/react';
import { Button } from '@/components/ui/button';
import { Card, CardContent, CardFooter, CardHeader, CardTitle } from '@/components/ui/card';
import { Input } from '@/components/ui/input';
import { Label } from '@/components/ui/label';
import { TextArea } from '@/components/ui/textArea';
import { Avatar, AvatarFallback, AvatarImage } from '@/components/ui/avatar';
import { useToast } from '@/components/ui/use-toast';
import Link from 'next/link';
import { Api, UsersApi } from '@game-guild/apiclient';

export default function EditProfile() {
  const [originalUser, setOriginalUser] = useState<Api.UserEntity>();
  const [user, setUser] = useState<Api.UserEntity>();
  const [usernameError, setUsernameError] = useState<string | null>(null);
  const [bioError, setBioError] = useState<string | null>(null);
  const [emailError, setEmailError] = useState<string | null>(null);

  const { toast } = useToast();
  const router = useRouter();

  const usersApi = new UsersApi({
    basePath: process.env.NEXT_PUBLIC_API_URL,
  });

  useLayoutEffect(() => {
    loadCurrentUser();
  }, []);

  const loadCurrentUser = async () => {
    const session = await getSession();
    console.log('session: ', session);
    if (!session) {
      router.push('/connect');
      return;
    }
    const response = await usersApi.userControllerMe({
      headers: { Authorization: `Bearer ${session.user?.accessToken}` },
    });
    console.log('response: ', response);
    if (response.status == 200) {
      console.log('response.body: ', response.body);
      setOriginalUser(response.body as Api.UserEntity);
      setUser(response.body as Api.UserEntity);
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
              <AvatarImage src={'/placeholder.svg'} alt="Profile picture" />
              <AvatarFallback>Avatar</AvatarFallback>
            </Avatar>
            <Label htmlFor="avatar-upload" className="cursor-pointer text-sm text-blue-500 hover:text-blue-600">
              Change Avatar
            </Label>
            <Input id="avatar-upload" type="file" accept="image/*" className="hidden" onChange={handleAvatarChange} />
          </div>
          <div className="space-y-2">
            <Label htmlFor="username">Username</Label>
            <Input
              id="username"
              value={user?.username}
              onChange={(e) => {
                // should match /^[a-z0-9_.-]{1,32}$/
                if (!/^[a-z0-9_.-]{3,32}$/.test(e.target.value))
                  setUsernameError(
                    'Username must contain only lowercase alphanumeric characters, underscores, periods, hyphens, and be between 3 and 32 characters.',
                  );
                else setUsernameError(null);
                setUser({
                  ...user,
                  username: e.target.value,
                } as Api.UserEntity);
              }}
              placeholder="Enter your username"
            />
            <p className="text-sm text-gray-500 mt-1">Publicly visible. Change it if want to stay anonymous.</p>
            {usernameError && <p className="text-sm text-red-500">{usernameError}</p>}
          </div>
          <div className="space-y-2">
            <Label htmlFor="email">Email</Label>
            <Input id="email" value={user?.email} disabled />
            <p className="text-sm text-gray-500 mt-1">
              Your email address is not publicly visible. Currently disabled, but you can code it to be editable{' '}
              <Link className={'text-blue-500 hover:text-blue-600'} href={'https://github.com/gameguild-gg/website/'}>
                here
              </Link>
              .
            </p>
          </div>
          <div className="space-y-2">
            <Label htmlFor="bio">Biography</Label>
            <TextArea
              id="bio"
              value={user?.profile?.bio}
              onChange={(e) => {
                if (e.target.value.length > 256) setBioError('Bio should be at most 256 characters');
                else setBioError(null);

                setUser({
                  ...user,
                  profile: { ...user?.profile, bio: e.target.value },
                } as Api.UserEntity);
              }}
              placeholder="Tell us about yourself"
              rows={4}
            />
            <p className="text-sm text-gray-500 mt-1">Introduce yourself to the community.</p>
            {bioError && <p className="text-sm text-red-500">{bioError}</p>}
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
