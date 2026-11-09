'use client';

import Link from 'next/link';
import { ContactRound, Landmark, Wallet } from 'lucide-react';
import { useAuth } from './auth-context';
import styles from './accounting-section-navigation.module.css';

export function BankingSectionNavigation({ active }: { active: 'clients' | 'banks' | 'accounts' }) {
  const { can } = useAuth();
  return (
    <nav className={styles.navigation} aria-label="Clients, banques et comptes bancaires">
      {[
        {
          key: 'clients',
          href: '/clients',
          label: 'Clients',
          icon: ContactRound,
          permission: 'clients.read',
        },
        {
          key: 'banks',
          href: '/banks',
          label: 'Banques',
          icon: Landmark,
          permission: 'banks.read',
        },
        {
          key: 'accounts',
          href: '/bank-accounts',
          label: 'Comptes bancaires',
          icon: Wallet,
          permission: 'accounts.read',
        },
      ]
        .filter((item) => can(item.permission))
        .map(({ key, href, label, icon: Icon }) => (
          <Link
            key={key}
            href={href}
            className={styles.link}
            aria-current={active === key ? 'page' : undefined}
          >
            <Icon size={18} strokeWidth={1.8} aria-hidden="true" />
            <span>{label}</span>
          </Link>
        ))}
    </nav>
  );
}
