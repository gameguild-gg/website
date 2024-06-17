import { useFormState } from 'react-dom';
import { SignUpFormState, signUpWithEmailAndPassword } from '@/lib/auth';

const initialState: SignUpFormState = {};

export function useSignUpFormState() {
  return useFormState(signUpWithEmailAndPassword, initialState);
}
