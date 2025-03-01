/** @type {import('tailwindcss').Config} */
module.exports = {
  darkMode: 'class',
  content: [
    './pages/**/*.{js,ts,jsx,tsx,mdx}',
    './components/**/*.{js,ts,jsx,tsx,mdx}',
    './app/**/*.{js,ts,jsx,tsx,mdx}',
  ],
  theme: {
    extend: {
      backgroundColor: {
        'light': '#FFFFFF',
        'dark': '#1E1E1E',
        'high-contrast': '#000000',
      },
      textColor: {
        'light': '#000000',
        'dark': '#D4D4D4',
        'high-contrast': '#FFFFFF',
      },
      borderColor: {
        'light': '#E5E7EB',
        'dark': '#3C3C3C',
        'high-contrast': '#FFFFFF',
      },
    },
  },
  variants: {
    extend: {
      backgroundColor: ['light', 'dark', 'high-contrast'],
      textColor: ['light', 'dark', 'high-contrast'],
      borderColor: ['light', 'dark', 'high-contrast'],
    },
  },
  plugins: [],
}

