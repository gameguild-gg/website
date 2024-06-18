export const environment = {
  API_URL: `${process.env.NEXT_PUBLIC_API_URL!}`,
  GoogleAnalyticsMeasurementId: `${process.env.GOOGLE_ANALYTICS_MEASUREMENT_ID!}`,
  GoogleClientId: `${process.env.GOOGLE_CLIENT_ID!}`,
  GoogleClientSecret: `${process.env.GOOGLE_CLIENT_SECRET!}`,
  GoogleProjectId: `${process.env.GOOGLE_PROJECT_ID!}`,
  GoogleTagManagerId: `${process.env.GOOGLE_TAG_MANAGER_ID!}`,
  SignInGoogleCallbackUrl: `${process.env.NEXT_PUBLIC_WEB_URL!}/api/auth/callback/google`,
};
