/** @type {import('tailwindcss').Config} */
module.exports = {
  content: [
    './Views/**/*.cshtml',
    './wwwroot/js/**/*.js',
  ],
  // Disable preflight to avoid interfering with existing Bootstrap + custom CSS
  corePlugins: {
    preflight: false,
  },
  theme: {
    extend: {},
  },
  plugins: [],
}
