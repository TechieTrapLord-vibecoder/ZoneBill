/** @type {import('tailwindcss').Config} */
module.exports = {
  content: [
    './Views/**/*.cshtml',
    './wwwroot/js/**/*.js',
  ],
  corePlugins: {
    preflight: false,
  },
  theme: {
    extend: {
      colors: {
        zb: {
          primary: '#0f172a',
          'primary-mid': '#1e293b',
          'primary-alt': '#334155',
          secondary: '#06b6d4',
          accent: '#f97316',
          text: '#f1f5f9',
          muted: '#94a3b8',
        },
      },
      animation: {
        'fade-up': 'fadeUp 0.7s ease-out forwards',
        'fade-up-delay-1': 'fadeUp 0.7s ease-out 0.1s forwards',
        'fade-up-delay-2': 'fadeUp 0.7s ease-out 0.2s forwards',
        'fade-up-delay-3': 'fadeUp 0.7s ease-out 0.3s forwards',
        'fade-up-delay-4': 'fadeUp 0.7s ease-out 0.4s forwards',
        'scale-in': 'scaleIn 0.6s ease-out forwards',
        'slide-left': 'slideLeft 0.7s ease-out forwards',
        'slide-right': 'slideRight 0.7s ease-out forwards',
        'glow-pulse': 'glowPulse 3s ease-in-out infinite',
        'shimmer': 'shimmer 2.5s linear infinite',
        'float-slow': 'floatSlow 6s ease-in-out infinite',
        'float-medium': 'floatMedium 5s ease-in-out infinite',
        'float-fast': 'floatFast 4s ease-in-out infinite',
        'gradient-shift': 'gradientShift 8s ease infinite',
        'border-glow': 'borderGlow 3s ease-in-out infinite',
        'counter-up': 'counterUp 1s ease-out forwards',
        'hero-badge-pulse': 'heroBadgePulse 2.5s ease-in-out infinite',
        'spin-slow': 'spin 12s linear infinite',
      },
      keyframes: {
        fadeUp: {
          '0%': { opacity: '0', transform: 'translateY(30px)' },
          '100%': { opacity: '1', transform: 'translateY(0)' },
        },
        scaleIn: {
          '0%': { opacity: '0', transform: 'scale(0.9)' },
          '100%': { opacity: '1', transform: 'scale(1)' },
        },
        slideLeft: {
          '0%': { opacity: '0', transform: 'translateX(40px)' },
          '100%': { opacity: '1', transform: 'translateX(0)' },
        },
        slideRight: {
          '0%': { opacity: '0', transform: 'translateX(-40px)' },
          '100%': { opacity: '1', transform: 'translateX(0)' },
        },
        glowPulse: {
          '0%, 100%': { boxShadow: '0 0 20px rgba(6,182,212,0.15)' },
          '50%': { boxShadow: '0 0 40px rgba(6,182,212,0.3), 0 0 80px rgba(6,182,212,0.1)' },
        },
        shimmer: {
          '0%': { backgroundPosition: '-200% center' },
          '100%': { backgroundPosition: '200% center' },
        },
        floatSlow: {
          '0%, 100%': { transform: 'translateY(0)' },
          '50%': { transform: 'translateY(-12px)' },
        },
        floatMedium: {
          '0%, 100%': { transform: 'translateY(0) rotate(0deg)' },
          '50%': { transform: 'translateY(-8px) rotate(1deg)' },
        },
        floatFast: {
          '0%, 100%': { transform: 'translateY(0)' },
          '50%': { transform: 'translateY(-6px)' },
        },
        gradientShift: {
          '0%, 100%': { backgroundPosition: '0% 50%' },
          '50%': { backgroundPosition: '100% 50%' },
        },
        borderGlow: {
          '0%, 100%': { borderColor: 'rgba(6,182,212,0.3)' },
          '50%': { borderColor: 'rgba(6,182,212,0.7)' },
        },
        counterUp: {
          '0%': { opacity: '0', transform: 'translateY(10px)' },
          '100%': { opacity: '1', transform: 'translateY(0)' },
        },
        heroBadgePulse: {
          '0%, 100%': { boxShadow: '0 0 0 0 rgba(249,115,22,0.4)' },
          '50%': { boxShadow: '0 0 0 8px rgba(249,115,22,0)' },
        },
      },
      backgroundSize: {
        '400%': '400% 400%',
      },
    },
  },
  plugins: [],
}
