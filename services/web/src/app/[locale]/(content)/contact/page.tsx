import React from 'react';
import {ParamsWithLocale} from "@/types";
import ContactForm from "@/components/contact/contact-form";

export async function generateStaticParams(): Promise<ParamsWithLocale[]> {
  return [];
}

export default async function Page() {
  return (
    <div>
      <h1>Contact</h1>
      <ContactForm/>
    </div>
  );
}
