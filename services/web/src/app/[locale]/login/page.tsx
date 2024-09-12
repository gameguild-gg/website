'use client';
import MetamaskSignIn from '@/components/others/web3/metamask';
import { useRouter } from 'next/navigation';
import { message, notification, NotificationArgsProps } from 'antd';
import React from 'react';
import { NotificationProvider } from '@/components/others/common/NotificationContext';
import { getCookies, setCookie, deleteCookie, getCookie } from 'cookies-next';
import {
  LocalSignInDto,
  LocalSignInResponseDto,
  LocalSignUpDto,
} from '@game-guild/apiclient';

enum UserExists {
  NotChecked = 'NotChecked',
  UserExists = 'UserExists',
  UserNotExists = 'UserNotExists',
}
function Home() {
  const [api, contextHolder] = notification.useNotification();
  const router = useRouter();

  const [emailOrUsername, setEmailOrUsername] = React.useState('');
  const [password, setPassword] = React.useState('');

  const [email, setEmail] = React.useState('');
  const [username, setUsername] = React.useState('');

  const [userExists, setUserExists] = React.useState(UserExists.NotChecked);

  const [userOrEmailLabel, setUserOrEmailLabel] =
    React.useState('Email or username');

  // password hidden
  const [passwordHidden, setPasswordHidden] = React.useState(true);

  // email hidden
  const [emailHidden, setEmailHidden] = React.useState(true);

  const [emailOrUsernameHidden, setEmailOrUsernameHidden] =
    React.useState(false);

  const [usernameHidden, setUsernameHidden] = React.useState(true);

  const [buttonText, setButtonText] = React.useState('Verify');

  const [emailOrUsernameLocked, setEmailOrUsernameLocked] =
    React.useState(false);

  const [emailLocked, setEmailLocked] = React.useState(false);

  const [usernameLocked, setUsernameLocked] = React.useState(false);

  // function to check if user exists. receives string usernameOrPassword
  const onButtonClick = async () => {
    if (userExists === UserExists.NotChecked) {
      // call the api to check if user exists
      const baseUrl = process.env.NEXT_PUBLIC_API_URL;
      const url = `${baseUrl}/auth/userExists/${emailOrUsername}`;

      const userExists: boolean = await (await fetch(url)).json();
      message.info(`User exists: ${userExists}`);

      if (userExists) {
        setUserExists(UserExists.UserExists);
        setPasswordHidden(false);
        setButtonText('Log in');
        setEmailOrUsernameLocked(true);
      } else {
        if (emailOrUsername.includes('@')) {
          setEmail(emailOrUsername);
          setEmailLocked(true);
        } else {
          setUsername(emailOrUsername);
          setUsernameLocked(true);
        }
        setUserExists(UserExists.UserNotExists);
        setEmailOrUsernameHidden(true);
        setUsernameHidden(false);
        setEmailHidden(false);
        setPasswordHidden(false);
        setButtonText('Sign up');
        setUserOrEmailLabel('Username');
      }
    } else if (userExists === UserExists.UserExists) {
      // sign in
      const baseUrl = process.env.NEXT_PUBLIC_API_URL;
      const url = `${baseUrl}/auth/local/sign-in`;
      let data: Partial<LocalSignInDto>;
      if (emailOrUsername.includes('@'))
        data = { email: emailOrUsername, password };
      else data = { username: emailOrUsername, password };
      const response = await fetch(url, {
        method: 'POST',
        headers: {
          'Content-Type': 'application/json',
        },
        body: JSON.stringify(data),
      });

      if (!response.ok) {
        const json = await response.json();
        message.error(`${JSON.stringify(json)}`);
        return;
      }

      const json: LocalSignInResponseDto = await response.json();

      // todo: store expiration date in cookie properly
      // todo: decode jwt token to get expiration date
      setCookie('access_token', json.accessToken, {
        path: '/',
      });
      setCookie('refresh_token', json.refreshToken, {
        path: '/',
      });
      setCookie('user', JSON.stringify(json.user), {
        path: '/',
      });

      router.push('../competition');
    } else if (userExists === UserExists.UserNotExists) {
      const baseUrl = process.env.NEXT_PUBLIC_API_URL;
      const url = `${baseUrl}/auth/local/sign-up`;
      const data: LocalSignUpDto = { email, username, password };

      const response = await fetch(url, {
        method: 'POST',
        headers: {
          'Content-Type': 'application/json',
        },
        body: JSON.stringify(data),
      });
      const responseJson = await response.json();
      if (!response.ok) {
        message.error(`${JSON.stringify(responseJson.error)}`);
        return;
      } else {
        message.success('User created successfully');
      }
      const json: LocalSignInResponseDto = responseJson;

      // todo: store expiration date in cookie properly
      // todo: decode jwt token to get expiration date
      setCookie('access_token', json.accessToken, {
        path: '/',
      });
      setCookie('refresh_token', json.refreshToken, {
        path: '/',
      });
      setCookie('user', JSON.stringify(json.user), {
        path: '/',
      });
      router.push('../competition');
    }
  };

  const openNotification = (
    description: string,
    message: string = 'info',
    placement: NotificationArgsProps['placement'] = 'topRight',
  ) => {
    notification.info({
      message,
      description: description,
      placement,
    });
  };

  const handleLoginGoogle = async () => {
    openNotification(
      'You just found a WiP feature. Help us finish by coding it for us, or you can pay us a beer or more.',
    );
    async function signInWithGoogle() {}

    try {
      await signInWithGoogle();
      // router.push('/dashboard'); // Redirecionar após o login
    } catch (error) {
      console.error('Error logging in with Google:', error);
    }
  };

  const handleLoginGitHub = async () => {
    openNotification(
      'You just found a WiP feature. Help us finish by coding it for us, or you can pay us a beer or more.',
    );
    async function signInWithGitHub() {}

    try {
      await signInWithGitHub();

      // await router.push('/dashboard'); // Redirecionar após o login
    } catch (error) {
      console.error('Error logging in with GitHub:', error);
    }
  };

  return (
    <main>
      <NotificationProvider>
        <div className="flex h-screen justify-center items-center">
          <div className="max-w-md w-full p-6 bg-white border rounded-lg shadow">
            <h1 className="text-2xl font-semibold mb-4">{buttonText}</h1>
            <div hidden={emailOrUsernameHidden}>
              <label htmlFor="username" className="block text-sm font-medium">
                Email or Username
              </label>
              <input
                style={{ color: 'black' }}
                type="text"
                onChange={(e) => setEmailOrUsername(e.target.value)}
                id="emailOrUsername"
                readOnly={emailOrUsernameLocked}
                className="mt-1 p-2 w-full border rounded-md"
                onKeyDown={(e) => {
                  if (e.key === 'Enter') onButtonClick();
                }}
              />
            </div>
            <div hidden={usernameHidden}>
              <label htmlFor="username" className="block text-sm font-medium">
                Username
              </label>
              <input
                style={{ color: 'black' }}
                type="text"
                onChange={(e) => setUsername(e.target.value)}
                id="username"
                defaultValue={username}
                readOnly={usernameLocked}
                className="mt-1 p-2 w-full border rounded-md"
              />
            </div>
            <div hidden={emailHidden}>
              <label htmlFor="email" className="block text-sm font-medium">
                Email
              </label>
              <input
                style={{ color: 'black' }}
                type="email"
                defaultValue={email}
                onChange={(e) => setEmail(e.target.value)}
                id="email"
                className="mt-1 p-2 w-full border rounded-md"
                readOnly={emailLocked}
              />
            </div>
            <div hidden={passwordHidden}>
              <label htmlFor="password" className="block text-sm font-medium">
                Password
              </label>
              <input
                style={{ color: 'black' }}
                type="password"
                id="password"
                onChange={(e) => setPassword(e.target.value)}
                className="mt-1 p-2 w-full border rounded-md"
                onKeyDown={(e) => {
                  if (e.key === 'Enter') onButtonClick();
                }}
              />
            </div>
            <button
              onClick={onButtonClick}
              className="w-full bg-blue-500 text-white p-2 rounded-md"
            >
              {buttonText}
            </button>
          </div>
        </div>
      </NotificationProvider>
    </main>
  );
}

export default Home;
