import NextAuth from "next-auth";
import {authConfig} from "@/config/auth.config";


export const {auth, handlers, signIn, signOut} = NextAuth({...authConfig});
