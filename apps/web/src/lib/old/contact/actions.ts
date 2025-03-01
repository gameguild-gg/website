'use server';

export type ContactFormState = {};

export async function submitContactForm(
  previousState: ContactFormState,
  formData: FormData,
): Promise<ContactFormState> {
  // TODO: Implement a contact form submission.
  return Promise.resolve({});
}
