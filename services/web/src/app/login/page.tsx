'use client';
import MetamaskSignIn from '@/components/web3/metamask';
import { useRouter } from 'next/navigation';
import {notification, NotificationArgsProps} from "antd";
import React from "react";
import {NotificationProvider} from "@/app/NotificationContext";


function Home() {
  const [api, contextHolder] = notification.useNotification();
  const router = useRouter();
  
  const [emailOrUsername, setEmailOrUsername] = React.useState('');
  const [password, setPassword] = React.useState('');

  const openNotification = (description:string, message:string="info", placement: NotificationArgsProps['placement'] = 'topRight') => {
    notification.info({
      message,
      description: description,
      placement,
    });
  };

  const handleLoginGoogle = async () => {
    openNotification('You just found a WiP feature. Help us finish by coding it for us, or you can pay us a beer or more.');
    async function signInWithGoogle() {}

    try {
      await signInWithGoogle();
      // router.push('/dashboard'); // Redirecionar após o login
    } catch (error) {
      console.error('Error logging in with Google:', error);
    }
  };

  const handleLoginGitHub = async () => {
    openNotification('You just found a WiP feature. Help us finish by coding it for us, or you can pay us a beer or more.');
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
          <h1 className="text-2xl font-semibold mb-4">Login</h1>
          <form className="space-y-4">
            <div>
              <label htmlFor="email" className="block text-sm font-medium">
                Email
              </label>
              <input
                type="text"
                id="email"
                className="mt-1 p-2 w-full border rounded-md"
              />
            </div>
            <div>
              <label htmlFor="password" className="block text-sm font-medium">
                Password
              </label>
              <input
                type="password"
                id="password"
                className="mt-1 p-2 w-full border rounded-md"
              />
            </div>
            <button
              type="submit"
              className="w-full bg-blue-500 text-white p-2 rounded-md"
            >
              Log in
            </button>
          </form>
          {/*<div className="mt-4">*/}
          {/*  <button*/}
          {/*    onClick={handleLoginGoogle}*/}
          {/*    className="w-full bg-red-500 text-white p-2 rounded-md"*/}
          {/*  >*/}
          {/*    Log in with Google*/}
          {/*  </button>*/}
          {/*  <button*/}
          {/*    onClick={handleLoginGitHub}*/}
          {/*    className="w-full bg-gray-800 text-white p-2 rounded-md mt-2"*/}
          {/*  >*/}
          {/*    Log in with GitHub*/}
          {/*  </button>*/}
          {/*</div>*/}
          {/*<div className="mt-4">*/}
          {/*  <MetamaskSignIn />*/}
          {/*</div>*/}
        </div>
      </div>
      </NotificationProvider>  
    </main>
  );
}

export default Home;
