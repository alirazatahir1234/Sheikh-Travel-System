/** @type {import('tailwindcss').Config} */
module.exports = {
  content: ['./src/**/*.{html,ts}'],
  theme: {
    extend: {
      fontFamily: {
        sans: ['Inter', 'Roboto', 'ui-sans-serif', 'system-ui', 'sans-serif']
      },
      colors: {
        page: '#edf0f2',
        surface: {
          DEFAULT: '#ffffff',
          alt: '#f6f8fa'
        },
        stroke: {
          DEFAULT: '#e5e9ef',
          strong: '#d5dbe3'
        },
        primary: {
          50: '#e8f4f2',
          100: '#c8ebe6',
          200: '#9dd9d2',
          300: '#6bc4b8',
          400: '#3da89a',
          500: '#2a9487',
          600: '#1b7f75',
          700: '#177066',
          800: '#125a52',
          900: '#0c433d'
        }
      },
      boxShadow: {
        card: '0 2px 8px rgba(15, 23, 42, 0.06)'
      },
      borderRadius: {
        stb: '16px',
        'stb-sm': '10px',
        'stb-lg': '20px'
      }
    }
  },
  plugins: []
};
