'use client';
import { useRef, useState } from 'react';
import { Copy, Download, FileText, Sparkles } from 'lucide-react';
import { Button } from '@/components/ui/button';

export function DashboardConversionResources() {
  const [prompt, setPrompt] = useState('');
  const [open, setOpen] = useState(false);
  const [busy, setBusy] = useState(false);
  const [message, setMessage] = useState('');
  const field = useRef<HTMLTextAreaElement>(null);
  async function loadPrompt() {
    if (prompt) return prompt;
    const response = await fetch('/resources/prompt-conversion-releve.txt');
    if (!response.ok) throw new Error('Le prompt ne peut pas être chargé. Réessayez ou téléchargez le fichier texte.');
    const text = await response.text();
    setPrompt(text);
    return text;
  }
  async function copyPrompt() {
    setBusy(true);
    setMessage('');
    try {
      const text = await loadPrompt();
      if (navigator.clipboard?.writeText) {
        try { await navigator.clipboard.writeText(text); setMessage('Prompt copié. Collez-le dans ChatGPT avec votre PDF et le modèle PDF.'); return; } catch { /* HTTP LAN or browser permissions: offer manual selection. */ }
      }
      setOpen(true);
      requestAnimationFrame(() => {
        field.current?.focus();
        field.current?.select();
        let copied = false;
        try { copied = document.execCommand('copy'); } catch { /* Keep manual selection available. */ }
        setMessage(copied ? 'Prompt copié.' : 'Texte sélectionné : utilisez Ctrl+C pour le copier.');
      });
    } catch (error) { setMessage((error as Error).message); }
    finally { setBusy(false); }
  }
  async function togglePrompt() {
    if (open) { setOpen(false); return; }
    try { await loadPrompt(); setOpen(true); } catch (error) { setMessage((error as Error).message); }
  }
  return (
    <section className="overview-card overview-resources" aria-labelledby="conversion-resources-title">
      <div className="overview-card-heading">
        <div>
          <h2 id="conversion-resources-title"><Sparkles size={18} /> Préparer un relevé avec ChatGPT</h2>
          <p>Joignez votre relevé source et le modèle PDF à ChatGPT, puis collez le prompt.</p>
        </div>
        <span className="overview-icon teal"><FileText size={20} /></span>
      </div>
      <div className="overview-resources-actions">
        <Button onClick={copyPrompt} disabled={busy}><Copy size={16} />{busy ? 'Chargement…' : 'Copier le prompt'}</Button>
        <Button variant="outline" asChild><a href="/resources/modele-releve-alias.pdf" download><Download size={16} />Modèle PDF · 5 colonnes</a></Button>
        <Button variant="outline" asChild><a href="/resources/Relever.ema" download><Download size={16} />Modèle EMA · Relever.ema</a></Button>
      </div>
      <div className="overview-resources-links">
        <button type="button" onClick={togglePrompt} aria-expanded={open} aria-controls="conversion-prompt">{open ? 'Masquer le prompt' : 'Lire le prompt'}</button>
        <a href="/resources/prompt-conversion-releve.txt" download>Télécharger le prompt (.txt)</a>
      </div>
      {open && <textarea ref={field} id="conversion-prompt" aria-label="Prompt de conversion pour ChatGPT" readOnly value={prompt} rows={15} />}
      <p role="status" className="overview-resources-status">{message || 'PDF multipage : cinq colonnes, sans colonne Référence, avec un seul solde de départ et un seul solde final.'}</p>
    </section>
  );
}
