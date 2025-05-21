export const environment = {
  apiBaseUrl: `${process.env.NEXT_PUBLIC_API_URL!}`,
  googleAnalyticsMeasurementId: `${process.env.GOOGLE_ANALYTICS_MEASUREMENT_ID!}`,
  googleClientId: `${process.env.GOOGLE_CLIENT_ID!}`,
  googleClientSecret: `${process.env.GOOGLE_CLIENT_SECRET!}`,
  googleProjectId: `${process.env.GOOGLE_PROJECT_ID!}`,
  googleTagManagerId: `${process.env.GOOGLE_TAG_MANAGER_ID!}`,
  signInGoogleCallbackUrl: `${process.env.NEXT_PUBLIC_WEB_URL!}/api/auth/callback/google`,
};
