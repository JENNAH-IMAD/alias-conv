'use client';

import Link from 'next/link';
import Image from 'next/image';
import { useEffect, useRef, useState, type CSSProperties, type PointerEvent } from 'react';
import {
  ArrowUpRight,
  ChevronLeft,
  ChevronRight,
  Copy,
  Eye,
  EyeOff,
  Landmark,
  Pencil,
  Plus,
  RotateCw,
  ShieldCheck,
  Trash2,
  Wallet,
} from 'lucide-react';
import { request, type Account, type Bank } from '@/services/api';
import { useAll } from '@/services/use-api';
import { Button } from './ui/button';
import styles from './bank-account-wallet.module.css';
import { bankCardTheme } from '@/lib/bank-card-theme';

type Props = {
  accounts: Account[];
  onEdit: (account: Account) => void;
  onDelete: (account: Account) => void;
  onNew: () => void;
  deleting: boolean;
};

function BankLogo({ bankId, bank, version, variant }: { bankId: string; bank: string; version: string | null; variant: 'brand' | 'detail' | 'selector' }) {
  const [logo, setLogo] = useState<{ key: string; src: string } | null>(null);
  const logoKey = `${bankId}:${version ?? ''}`;
  useEffect(() => {
    let active = true;
    if (version) {
      request<{ dataUrl: string | null }>(`/banks/${encodeURIComponent(bankId)}/logo`)
        .then((result) => {
          if (active && result.dataUrl) setLogo({ key: logoKey, src: result.dataUrl });
        })
        .catch(() => {});
    }
    return () => { active = false; };
  }, [bankId, logoKey, version]);
  const logoSrc = logo?.key === logoKey ? logo.src : null;
  const theme = bankCardTheme(bank);
  return (
    <span className={styles[`${variant}BankLogo`]} data-bank={theme.key} style={{ '--logo-fallback': theme.background } as CSSProperties}>
      {logoSrc ? (
        <Image
          src={logoSrc}
          alt={`Logo ${bank}`}
          width={96}
          height={96}
          unoptimized
          onError={() => setLogo((current) => current?.key === logoKey ? null : current)}
        />
      ) : (
        <Landmark aria-hidden="true" />
      )}
    </span>
  );
}

function themeStyle(bank: string, accountId: string): CSSProperties {
  const theme = bankCardTheme(bank);
  // Stable per-account tint: keep the bank's hues, with a distinct finish for each account.
  const hash = Array.from(accountId).reduce(
    (value, char) => (value * 31 + char.charCodeAt(0)) >>> 0,
    0,
  );
  const shade = (hash % 25) / 100;
  const angle = 105 + (hash % 65);
  return {
    '--card-skin': `linear-gradient(${angle}deg, rgb(0 0 0 / ${shade}), transparent 70%), ${theme.background}`,
    '--card-ink': theme.ink,
  } as CSSProperties;
}

export function BankAccountWallet({ accounts, onEdit, onDelete, onNew, deleting }: Props) {
  const { data: banks } = useAll<Bank>('/banks');
  const bankVersions = new Map((banks ?? []).map((bank) => [bank.id, bank.logoVersion]));
  const [selectedId, setSelectedId] = useState(accounts[0].id);
  const index = Math.max(
    0,
    accounts.findIndex((account) => account.id === selectedId),
  );
  const selected = accounts[index];
  const [flipped, setFlipped] = useState(false);
  const [revealed, setRevealed] = useState(false);
  const [feedback, setFeedback] = useState('');
  const [dragOffset, setDragOffset] = useState(0);
  const [dragging, setDragging] = useState(false);
  const drag = useRef<{ id: number; x: number; y: number; step: number; moved: boolean } | null>(
    null,
  );
  const suppressClick = useRef(false);
  const deck = useRef<HTMLDivElement>(null);

  function select(next: number) {
    const bounded = Math.max(0, Math.min(accounts.length - 1, next));
    if (bounded !== index) {
      setSelectedId(accounts[bounded].id);
      setFlipped(false);
      setRevealed(false);
      setFeedback('');
    }
  }
  function start(event: PointerEvent<HTMLDivElement>) {
    if (event.button !== 0) return;
    const cardWidth =
      deck.current?.querySelector('button[data-active="true"]')?.getBoundingClientRect().width ??
      300;
    suppressClick.current = false;
    drag.current = {
      id: event.pointerId,
      x: event.clientX,
      y: event.clientY,
      step: cardWidth * 0.74,
      moved: false,
    };
  }
  function move(event: PointerEvent<HTMLDivElement>) {
    const current = drag.current;
    if (!current || event.pointerId !== current.id) return;
    const dx = event.clientX - current.x;
    if (!current.moved) {
      if (
        Math.abs(event.clientY - current.y) > Math.abs(dx) &&
        Math.abs(event.clientY - current.y) > 8
      ) {
        drag.current = null;
        return;
      }
      if (Math.abs(dx) < 8) return;
      current.moved = true;
      setDragging(true);
      event.currentTarget.setPointerCapture(event.pointerId);
      setFlipped(false);
    }
    const position = index - dx / current.step;
    const constrained = Math.max(-0.2, Math.min(accounts.length - 0.8, position));
    setDragOffset(index - constrained);
  }
  function end(event: PointerEvent<HTMLDivElement>, cancelled = false) {
    const current = drag.current;
    if (!current || event.pointerId !== current.id) return;
    suppressClick.current = current.moved;
    if (current.moved && !cancelled) {
      const dx = event.clientX - current.x;
      const amount =
        Math.abs(dx / current.step) > 0.18
          ? Math.sign(dx) * Math.max(1, Math.round(Math.abs(dx / current.step)))
          : 0;
      select(index - amount);
    }
    drag.current = null;
    setDragging(false);
    setDragOffset(0);
  }
  async function copy() {
    try {
      await navigator.clipboard.writeText(selected.accountNumber);
      setFeedback('Numéro de compte copié.');
    } catch {
      setFeedback('Copie indisponible. Affichez le numéro pour le sélectionner.');
      setRevealed(true);
    }
  }

  return (
    <div className={styles.layout} data-account-wallet>
      <section className={styles.stage} aria-label="Portefeuille des comptes bancaires">
        <div className={styles.stageHeading}>
          <span>
            <Wallet size={17} /> Vos comptes
          </span>
          <span>
            {index + 1} / {accounts.length}
          </span>
        </div>
        <div
          ref={deck}
          className={styles.deck}
          data-dragging={dragging || undefined}
          role="group"
          aria-roledescription="carrousel"
          aria-label="Comptes bancaires : flèches pour changer de compte, Entrée sur la carte pour la retourner"
          tabIndex={0}
          onPointerDown={start}
          onPointerMove={move}
          onPointerUp={(event) => end(event)}
          onPointerCancel={(event) => end(event, true)}
          onKeyDown={(event) => {
            if (event.key === 'ArrowRight' || event.key === 'ArrowLeft') {
              event.preventDefault();
              select(index + (event.key === 'ArrowRight' ? 1 : -1));
            }
          }}
        >
          {accounts.map((account, i) => {
            const offset = i - index + dragOffset;
            const active = i === index;
            return (
              <button
                key={account.id}
                type="button"
                className={styles.bankCard}
                data-bank={bankCardTheme(account.bank).key}
                data-active={active}
                tabIndex={active ? 0 : -1}
                aria-label={`${account.accountName}, ${account.bank}, se termine par ${account.accountNumber.slice(-4)}. ${active ? 'Retourner le compte' : 'Sélectionner le compte'}`}
                aria-pressed={active && flipped}
                style={
                  {
                    ...themeStyle(account.bank, account.id),
                    '--offset-x': `${offset * 74}%`,
                    '--scale': 1 - Math.min(Math.abs(offset), 2) * 0.12,
                    opacity: Math.abs(offset) > 2.3 ? 0 : 1 - Math.min(Math.abs(offset), 2) * 0.22,
                    zIndex: 20 - Math.round(Math.abs(offset) * 5),
                    visibility: Math.abs(offset) > 3 ? 'hidden' : 'visible',
                  } as CSSProperties
                }
                onClick={() => {
                  if (suppressClick.current) {
                    suppressClick.current = false;
                    return;
                  }
                  if (active) setFlipped(!flipped);
                  else select(i);
                }}
              >
                <span className={styles.flip} data-flipped={active && flipped}>
                  <span className={`${styles.face} ${styles.front}`} aria-hidden="true">
                    <span className={styles.cardTop}>
                      <span className={styles.cardBrand}>
                        <BankLogo bankId={account.bankId} bank={account.bank} version={bankVersions.get(account.bankId) ?? null} variant="brand" /> {bankCardTheme(account.bank).label}
                        <span className={styles.brandDot}>●</span>
                      </span>
                      <span className={styles.currency}>{account.currency}</span>
                    </span>
                    <span className={styles.cardBank}>{account.bank}</span>
                    <span className={styles.cardMiddle}>
                      <span className={styles.chip}>
                        <i />
                        <i />
                        <i />
                      </span>
                      <span className={styles.accountKind}>COMPTE BANCAIRE</span>
                    </span>
                    <span className={styles.maskedNumber}>
                      •••• <span>{account.accountNumber.slice(-4)}</span>
                    </span>
                    <span className={styles.cardBottom}>
                      <span>{account.accountName}</span>
                      <span className={styles.cardMark}>
                        <i />
                        <i />
                      </span>
                    </span>
                  </span>
                  <span className={`${styles.face} ${styles.back}`} aria-hidden="true">
                    <span className={styles.backBand} />
                    <span className={styles.backContent}>
                      <span className={styles.backLabel}>Numéro de compte</span>
                      <span className={styles.backNumber}>
                        {active && revealed
                          ? account.accountNumber
                          : `•••• •••• •••• ${account.accountNumber.slice(-4)}`}
                      </span>
                      <span className={styles.backParameters}>
                        <span>
                          Journal<b>{account.journal || 'Non renseigné'}</b>
                        </span>
                        <span>
                          Compte comptable<b>{account.accountCode || 'Non renseigné'}</b>
                        </span>
                      </span>
                      <span className={styles.backFoot}>
                        <ShieldCheck size={15} /> Données du compte · {account.currency}
                      </span>
                    </span>
                  </span>
                </span>
              </button>
            );
          })}
        </div>
        <div className={styles.navigation}>
          <button
            type="button"
            aria-label="Compte précédent"
            disabled={index === 0}
            onClick={() => select(index - 1)}
          >
            <ChevronLeft size={18} />
          </button>
          <span className={styles.counter}>
            Compte {index + 1} sur {accounts.length}
          </span>
          <button
            type="button"
            aria-label="Compte suivant"
            disabled={index === accounts.length - 1}
            onClick={() => select(index + 1)}
          >
            <ChevronRight size={18} />
          </button>
        </div>
        <p className={styles.hint}>
          Glissez pour parcourir · Cliquez sur la carte pour la retourner
        </p>
        <div className={styles.strip} aria-label="Sélectionner un compte">
          {accounts.map((account, i) => (
            <button
              key={account.id}
              type="button"
              aria-pressed={i === index}
              className={styles.selector}
              onClick={() => select(i)}
            >
              <BankLogo bankId={account.bankId} bank={account.bank} version={bankVersions.get(account.bankId) ?? null} variant="selector" />
              <span>
                <b>{account.accountName}</b>
                <small>•••• {account.accountNumber.slice(-4)}</small>
              </span>
            </button>
          ))}
          <Button
            permission="accounts.write"
            variant="ghost"
            className={styles.newAccount}
            onClick={onNew}
          >
            <Plus size={17} /> Ajouter
          </Button>
        </div>
        <span className="sr-only" aria-live="polite">
          {selected.accountName}, compte {index + 1} sur {accounts.length}
        </span>
      </section>
      <section className={styles.panel} aria-label="Détails du compte sélectionné">
        <div className={styles.panelLabel}>
          <Landmark size={17} /> Détails du compte
        </div>
        <div key={selected.id} className={styles.details}>
          <header className={styles.detailHeading}>
            <BankLogo bankId={selected.bankId} bank={selected.bank} version={bankVersions.get(selected.bankId) ?? null} variant="detail" />
            <div>
              <h2>{selected.accountName}</h2>
              <p>{selected.bank}</p>
            </div>
            <span className={styles.currencyTag}>{selected.currency}</span>
          </header>
          <div className={styles.detailSection}>
            <div className={styles.labelRow}>
              <h3>Numéro de compte</h3>
              <span>Se termine par {selected.accountNumber.slice(-4)}</span>
            </div>
            <p className={styles.fullNumber}>
              {revealed
                ? selected.accountNumber
                : `•••• •••• •••• ${selected.accountNumber.slice(-4)}`}
            </p>
            <div className={styles.revealActions}>
              <Button
                variant="outline"
                size="sm"
                aria-pressed={revealed}
                onClick={() => {
                  setRevealed(!revealed);
                  setFlipped(!revealed);
                }}
              >
                {revealed ? <EyeOff /> : <Eye />}
                {revealed ? 'Masquer' : 'Afficher le numéro'}
              </Button>
              <Button variant="ghost" size="sm" onClick={() => void copy()}>
                <Copy /> Copier
              </Button>
              <Button
                variant="ghost"
                size="icon-sm"
                aria-label="Retourner la carte"
                aria-pressed={flipped}
                onClick={() => setFlipped(!flipped)}
              >
                <RotateCw />
              </Button>
            </div>
            <p role="status" className={styles.feedback}>
              {feedback}
            </p>
          </div>
          <div className={styles.detailSection}>
            <h3>Paramètres comptables</h3>
            <dl className={styles.parameters}>
              <div>
                <span className={styles.parameterIcon}>
                  <Wallet size={17} />
                </span>
                <dt>
                  Journal comptable<small>Journal associé à ce compte</small>
                </dt>
                <dd>{selected.journal || 'Non renseigné'}</dd>
              </div>
              <div>
                <span className={styles.parameterIcon}>
                  <Landmark size={17} />
                </span>
                <dt>
                  Compte général<small>Compte de trésorerie</small>
                </dt>
                <dd>{selected.accountCode || 'Non renseigné'}</dd>
              </div>
            </dl>
          </div>
          <div className={styles.detailSection}>
            <div className={styles.ready}>
              <ShieldCheck size={19} />
              <span>
                <b>
                  {selected.journal && selected.accountCode
                    ? 'Paramètres comptables renseignés'
                    : 'Paramètres à compléter'}
                </b>
                <small>
                  {selected.journal && selected.accountCode
                    ? 'Consultez les relevés associés au dossier client.'
                    : 'Renseignez le journal et le compte général dans la fiche du compte.'}
                </small>
              </span>
            </div>
          </div>
          <div className={styles.actions}>
            <Button asChild>
              <Link href={`/client-workspace?clientId=${encodeURIComponent(selected.clientId)}`}>
                Voir les relevés <ArrowUpRight />
              </Link>
            </Button>
            <Button permission="accounts.write" variant="outline" onClick={() => onEdit(selected)}>
              <Pencil /> Modifier
            </Button>
          </div>
          <div className={styles.deleteRow}>
            <span>Gestion du compte bancaire</span>
            <Button
              permission="accounts.write"
              variant="ghost"
              size="sm"
              disabled={deleting}
              className="text-destructive"
              onClick={() => onDelete(selected)}
            >
              <Trash2 /> Supprimer
            </Button>
          </div>
        </div>
      </section>
    </div>
  );
}
