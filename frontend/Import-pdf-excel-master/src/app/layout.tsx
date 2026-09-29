import type { Metadata } from 'next';
import { Inter, Manrope } from 'next/font/google';
import './globals.css';
import { AppShell } from '@/components/app-shell';
import { AuthProvider } from '@/components/auth-context';
import { ThemeProvider } from '@/components/theme-context';

const inter = Inter({
  variable: '--font-inter',
  subsets: ['latin'],
});

const manrope = Manrope({
  variable: '--font-manrope',
  subsets: ['latin'],
});

export const metadata: Metadata = {
  title: 'Alias Convert PDF — Alias Informatique',
  description: 'Dashboard de gestion des relevés PDF vers Excel pour plusieurs clients et banques.',
};

export default function RootLayout({ children }: { children: React.ReactNode }) {
  return (
    <html
      lang="fr"
      suppressHydrationWarning
      className={`${inter.variable} ${manrope.variable} h-full antialiased`}
    >
      <body className="min-h-full">
        <ThemeProvider>
          <AuthProvider>
            <AppShell>{children}</AppShell>
          </AuthProvider>
        </ThemeProvider>
      </body>
    </html>
  );
}
