"use client";

import React from "react";
import { useFormState } from "react-dom";
import { SignUpFormState, signUpWithEmailAndPassword } from "@/lib/auth/actions";
import { Label } from "@/components/ui/label";
import { Input } from "@/components/ui/input";
import Link from "next/link";
import { Button } from "@/components/ui/button";
import { SubmitButton } from "../ui/submit-button";

const initialState: SignUpFormState = {};

export default function SignUpForm() {
  const [state, formAction] = useFormState(signUpWithEmailAndPassword, initialState);

  return (
    <form action={formAction}>
      <div className="mx-auto grid w-[350px] gap-6">
        <div className="grid gap-2 text-center">
          <h1 className="text-3xl font-bold">Sign Up</h1>
          <p className="text-balance text-muted-foreground">
            Enter your information to create an account
          </p>
        </div>
        <div className="grid gap-4">
          <div className="grid gap-2">
            <Label htmlFor="email">Email</Label>
            <Input id="email" type="email" placeholder="m@example.com" required />
          </div>
          <div className="grid gap-2">
            <div className="flex items-center h-5">
            <Label htmlFor="password">Password</Label>
            </div>
            <Input id="password" type="password" required />
          </div>
          <SubmitButton className="w-full">Sign Up</SubmitButton>
          <Button variant="outline" className="w-full">
            <img
              src="assets/images/google-icon.svg"
              className="w-[20px] h-[20px] m-2"
            />
            Sign Up with Google
            </Button>
        </div>
        <div className="mt-4 text-center text-sm">
          Already have an account?{" "}
          <Link href="/sign-in" className="underline">
            Sign in
          </Link>
        </div>
      </div>
    </form>
  );
}
