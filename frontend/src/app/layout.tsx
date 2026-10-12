import type { Metadata } from 'next';
import localFont from 'next/font/local';
import './globals.css';
import './workspace-panels.css';
import './sidebar-navigation.css';
import './conversion-studio.css';
import './workspace-access.css';
import './interface-design.css';
import './conversion-experience.css';
import './statement-pdf.css';
import './compact-workspace.css';
import './dashboard.css';
import { AuthProvider } from '@/components/auth-context';
import { AppShell } from '@/components/app-shell';
import { ThemeProvider } from '@/components/theme-context';
const inter = localFont({
  src: '../../assets/fonts/Inter-Latin.woff2',
  weight: '100 900',
  variable: '--font-inter',
  display: 'swap',
});
const manrope = localFont({
  src: '../../assets/fonts/Manrope-Latin.woff2',
  weight: '200 800',
  variable: '--font-manrope',
  display: 'swap',
});
export const metadata: Metadata = {
  title: 'Alias',
  applicationName: 'Alias',
  icons: {
    icon: { url: '/alias-icon.svg?v=2', type: 'image/svg+xml', sizes: 'any' },
  },
  description: 'Gestion des clients, banques, comptes et documents PDF.',
};
export default function RootLayout({ children }: { children: React.ReactNode }) {
  return (
    <html
      lang="fr"
      suppressHydrationWarning
      className={`${inter.variable} ${manrope.variable} h-full antialiased`}
    >
      {/* Browser extensions may add body attributes before React hydrates (cz-shortcut-listen). */}
      <body className="min-h-full" suppressHydrationWarning>
        <ThemeProvider>
          <AuthProvider>
            <AppShell>{children}</AppShell>
          </AuthProvider>
        </ThemeProvider>
      </body>
    </html>
  );
}
