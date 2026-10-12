'use client';
import { useState } from 'react';
import { KeyRound, ScanLine, Save, Server, ShieldCheck } from 'lucide-react';
import { useApi } from '@/services/use-api';
import { request } from '@/services/api';
import { useAuth } from '@/components/auth-context';
import { Button } from '@/components/ui/button';
import { LoadState } from '@/components/ui/load-state';
import { PageHeader } from '@/components/page-header';
import { useUnsavedChanges } from '@/lib/use-unsaved-changes';

type OcrSettings = {
  provider: 'LOCAL' | 'AI_STUDIO';
  cloudModel: string;
  baseUrl: string;
  hasToken: boolean;
  apiUrl: string;
};
export default function OcrSettingsPage() {
  const { user } = useAuth();
  const api = useApi<OcrSettings>(user?.role === 'Admin' ? '/settings/ocr' : null);
  if (user?.role !== 'Admin') return null;
  return (
    <div className="ui-page">
      <PageHeader
        icon={ScanLine}
        eyebrow="Administration"
        title="Configuration PaddleOCR"
        description="Configurez le moteur de lecture partagé par les utilisateurs de l’application."
      />
      <LoadState loading={api.loading} error={api.error} />
      {api.error && (
        <Button variant="outline" onClick={api.reload}>
          Réessayer
        </Button>
      )}
      {api.data && <OcrSettingsForm key={JSON.stringify(api.data)} initial={api.data} />}
    </div>
  );
}
function OcrSettingsForm({ initial }: { initial: OcrSettings }) {
  const [provider, setProvider] = useState(initial.provider);
  const [model, setModel] = useState(initial.cloudModel);
  const [baseUrl, setBaseUrl] = useState(initial.baseUrl);
  const [token, setToken] = useState('');
  const [saved, setSaved] = useState(initial);
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState('');
  const [notice, setNotice] = useState('');
  const dirty =
    provider !== saved.provider ||
    model !== saved.cloudModel ||
    baseUrl !== saved.baseUrl ||
    token !== '';
  useUnsavedChanges(dirty);
  // The API never returns the token, including after saving.
  return (
    <form
      className="ui-card space-y-6 max-w-3xl"
      onSubmit={async (event) => {
        event.preventDefault();
        if (busy) return;
        setBusy(true);
        setError('');
        setNotice('');
        try {
          const settings = await request<OcrSettings>('/settings/ocr', {
            method: 'PUT',
            body: JSON.stringify({
              provider,
              cloudModel: model,
              baseUrl,
              cloudToken: token.trim() || null,
            }),
          });
          setSaved(settings);
          setProvider(settings.provider);
          setModel(settings.cloudModel);
          setBaseUrl(settings.baseUrl);
          setToken('');
          setNotice(
            'Configuration enregistrée. Elle s’applique aux nouvelles conversions, sans redémarrage.',
          );
        } catch (error) {
          setError((error as Error).message);
        } finally {
          setBusy(false);
        }
      }}
    >
      <div className="flex items-start gap-3">
        <ShieldCheck className="accent-text shrink-0" size={22} />
        <div>
          <h2 className="text-lg font-semibold">Accès administrateur</h2>
          <p className="muted text-sm mt-1">
            Le jeton est enregistré chiffré côté serveur. Il n’est jamais renvoyé au navigateur ni
            affiché aux collaborateurs.
          </p>
        </div>
      </div>
      <fieldset disabled={busy} className="space-y-5">
        <label className="block">
          Moteur de lecture
          <select
            aria-label="Moteur de lecture"
            className="ui-input mt-2"
            value={provider}
            onChange={(event) => setProvider(event.target.value as OcrSettings['provider'])}
          >
            <option value="AI_STUDIO">API PaddleOCR AI Studio</option>
            <option value="LOCAL">PaddleOCR local</option>
          </select>
        </label>
        {provider === 'AI_STUDIO' ? (
          <>
            <div className="rounded-xl border p-4 space-y-2">
              <p className="flex items-center gap-2 text-sm font-semibold">
                <Server size={16} />
                API officielle PaddleOCR
              </p>
              <p className="muted text-xs break-all">{initial.apiUrl}</p>
              <p className="muted text-sm">
                Le serveur doit accéder à Internet. Les PDF nécessitant l’OCR seront transmis à ce
                service ; un jeton valide et du quota sont nécessaires.
              </p>
              <a
                className="accent-text text-sm underline"
                href="https://aistudio.baidu.com/paddleocr"
                target="_blank"
                rel="noreferrer"
              >
                Ouvrir PaddleOCR AI Studio pour obtenir un jeton
              </a>
            </div>
            <label className="block">
              Modèle PaddleOCR
              <select
                aria-label="Modèle PaddleOCR"
                className="ui-input mt-2"
                value={model}
                onChange={(event) => setModel(event.target.value)}
              >
                <option value="PP-OCRv6">PP-OCRv6 — texte et positions</option>
                <option value="PaddleOCR-VL-1.6">
                  PaddleOCR-VL-1.6 — tableaux et mise en page
                </option>
              </select>
            </label>
            <label className="block">
              <span className="flex items-center gap-2">
                <KeyRound size={16} />
                Jeton API PaddleOCR
              </span>
              <input
                aria-label="Jeton API PaddleOCR"
                className="ui-input mt-2"
                type="password"
                autoComplete="new-password"
                spellCheck={false}
                maxLength={4096}
                value={token}
                onChange={(event) => setToken(event.target.value)}
                required={!saved.hasToken}
                placeholder={
                  saved.hasToken
                    ? 'Laisser vide pour conserver le jeton enregistré'
                    : 'Saisir votre jeton AI Studio'
                }
              />
              <span className="muted text-xs mt-2 block">
                {saved.hasToken
                  ? 'Un jeton est déjà configuré. Saisissez une nouvelle valeur pour le remplacer.'
                  : 'Aucun jeton configuré.'}
              </span>
            </label>
          </>
        ) : (
          <label className="block">
            Adresse du moteur OCR local
            <input
              aria-label="Adresse du moteur OCR local"
              className="ui-input mt-2"
              type="url"
              required
              value={baseUrl}
              onChange={(event) => setBaseUrl(event.target.value)}
            />
            <span className="muted text-xs mt-2 block">
              Exemple : http://127.0.0.1:8091. Le service PaddleOCR doit être démarré et accessible
              depuis le serveur.
            </span>
          </label>
        )}
      </fieldset>
      {error && (
        <p role="alert" className="text-destructive">
          {error}
        </p>
      )}
      {notice && (
        <p role="status" className="text-sm accent-text">
          {notice}
        </p>
      )}
      <div className="flex flex-wrap items-center justify-between gap-3">
        <p className="muted text-xs">Les conversions en cours conservent leur configuration.</p>
        <Button disabled={busy || !dirty}>
          <Save size={16} />
          {busy ? 'Enregistrement…' : 'Enregistrer la configuration'}
        </Button>
      </div>
    </form>
  );
}
