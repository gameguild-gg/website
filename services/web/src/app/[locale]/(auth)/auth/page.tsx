'use client';
import {
  CredentialResponse,
  GoogleLogin,
  GoogleOAuthProvider,
} from '@react-oauth/google';
import React, { useMemo, useState } from 'react';
import { jwtDecode } from 'jwt-decode';

export default function Auth() {
  const [credentialResponse, setCredentialResponse] =
    useState<CredentialResponse | null>();

  const user = useMemo(() => {
    if (!credentialResponse?.credential) return;
    return jwtDecode(credentialResponse.credential);
  }, [credentialResponse?.credential]);

  // TODO: improve this for god's sake!
  // google outh provider should be on the root layout, not here!!
  // I will store all data on the local storage, and then I will use it on other locations
  return (
    <GoogleOAuthProvider clientId="118778425878-5fmaq87pg8scc7e5i51oor0vuj44hrmr.apps.googleusercontent.com">
      <GoogleLogin
        onSuccess={(credentialResponse) => {
          setCredentialResponse(credentialResponse);
          const jwt = jwtDecode(credentialResponse.credential || '');
          console.log('Login Success:', jwt);
          localStorage.setItem('jwt-google', JSON.stringify(jwt));
        }}
        onError={() => {
          console.log('Login Failed');
        }}
      />
    </GoogleOAuthProvider>
  );
}
