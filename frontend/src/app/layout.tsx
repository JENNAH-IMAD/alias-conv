import type { Metadata } from 'next';
import localFont from 'next/font/local';
import './globals.css';
// L'ordre conserve la cascade des styles partagés et des adaptations par écran.
import '../styles/workspace-panels.css';
import '../styles/sidebar-navigation.css';
import '../styles/conversion-studio.css';
import '../styles/workspace-access.css';
import '../styles/interface-design.css';
import '../styles/conversion-experience.css';
import '../styles/statement-pdf.css';
import '../styles/compact-workspace.css';
import '../styles/dashboard.css';
import '../styles/statement-tables.css';
import '../styles/history-design.css';
import '../styles/user-editor.css';
import '../styles/conversion-entry.css';
import '../styles/professional-layout.css';
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
