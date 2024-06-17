import { useFormState } from 'react-dom';
import { SignInFormState, signInWithEmailAndPassword } from '@/lib/auth';

const initialState: SignInFormState = {};

export function useSignInFormState() {
  return useFormState(signInWithEmailAndPassword, initialState);
}
