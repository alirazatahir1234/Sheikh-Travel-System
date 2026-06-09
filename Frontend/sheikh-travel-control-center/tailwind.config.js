/** @type {import('tailwindcss').Config} */
module.exports = {
  content: [
    "./src/**/*.{html,ts}",
  ],
  theme: {
    extend: {
      colors: {
        primary: {
          50: '#ECFDF5',
          100: '#D1FAE5',
          200: '#A7F3D0',
          300: '#6EE7B7',
          400: '#34D399',
          500: '#10B981',
          600: '#0F766E',
          700: '#0D6B64',
          800: '#065F46',
          900: '#064E3B',
        },
        surface: {
          DEFAULT: '#FFFFFF',
          alt: '#F8FAFC',
          muted: '#F4F7FA',
        },
        border: {
          DEFAULT: '#D8E0EA',
          strong: '#C5D0DE',
        },
        text: {
          DEFAULT: '#0F172A',
          muted: '#64748B',
          soft: '#94A3B8',
        },
      },
      fontFamily: {
        sans: ['Inter', 'Roboto', '-apple-system', 'BlinkMacSystemFont', 'Segoe UI', 'sans-serif'],
      },
      borderRadius: {
        'sm': '10px',
        'DEFAULT': '16px',
        'lg': '20px',
      },
      boxShadow: {
        'sm': '0 1px 2px rgba(15, 23, 42, 0.04)',
        'DEFAULT': '0 1px 2px rgba(15, 23, 42, 0.04), 0 8px 24px rgba(15, 23, 42, 0.06)',
        'lg': '0 2px 4px rgba(15, 23, 42, 0.05), 0 12px 32px rgba(15, 23, 42, 0.1)',
        'hover': '0 4px 12px rgba(15, 23, 42, 0.08), 0 16px 40px rgba(15, 118, 110, 0.12)',
      },
    },
  },
  plugins: [],
}

