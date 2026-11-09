'use client';
import { useId, useState } from 'react';
import {
  AlertCircle,
  ArrowRight,
  FileText,
  Table2,
  Check,
  CheckCircle2,
  ChevronDown,
  Cloud,
  Cpu,
  Eye,
  EyeOff,
  KeyRound,
  LoaderCircle,
  LockKeyhole,
  RotateCcw,
  ScanLine,
  Save,
  ShieldCheck,
  Sparkles,
} from 'lucide-react';
import { useApi } from '@/services/use-api';
import { request } from '@/services/api';
import { useAuth } from '@/components/auth-context';
import { Button } from '@/components/ui/button';
import { PageHeader } from '@/components/page-header';
import { LoadState } from '@/components/ui/load-state';
import { useUnsavedChanges } from '@/lib/use-unsaved-changes';

type OcrSettings = {
  provider: 'AUTO' | 'LOCAL' | 'AI_STUDIO';
  autoPaddleProvider?: 'LOCAL' | 'AI_STUDIO';
  cloudModel: string;
  baseUrl: string;
  hasToken: boolean;
  apiUrl: string;
};
type AvailableProvider = OcrSettings['provider'];
const engines = [
  {
    value: 'AUTO',
    name: 'Automatique',
    description: 'Texte natif, OCR et reconstruction des tableaux.',
    icon: Sparkles,
    tag: 'Recommandé',
  },
  {
    value: 'LOCAL',
    name: 'PaddleOCR local',
    description: 'Lecture sur votre serveur, sans clé API.',
    icon: Cpu,
    tag: 'Local',
  },
  {
    value: 'AI_STUDIO',
    name: 'PaddleOCR AI Studio',
    description: 'Lecture distante avec votre jeton PaddleOCR.',
    icon: Cloud,
    tag: 'API distante',
  },
] as const;

export default function OcrSettingsPage() {
  const { user } = useAuth();
  const api = useApi<OcrSettings>(user?.role === 'Admin' ? '/settings/ocr' : null);
  if (user?.role !== 'Admin')
    return (
      <div className="ui-page settings-page">
        <PageHeader
          eyebrow="Configuration OCR"
          title="Accès administrateur requis"
          description="Ces paramètres sont réservés aux administrateurs de l’espace de travail."
          icon={ShieldCheck}
        />
      </div>
    );
  return (
    <div className="ui-page settings-page">
      <PageHeader
        eyebrow="Configuration OCR"
        title="Moteurs de lecture"
        description="Une lecture adaptée à chaque relevé et extrait bancaire."
        icon={ScanLine}
        actions={
          <span className="settings-admin-badge">
            <ShieldCheck size={14} />
            Réglages partagés
          </span>
        }
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

function SecretField({
  label,
  value,
  configured,
  required,
  onChange,
}: {
  label: string;
  value: string;
  configured: boolean;
  required: boolean;
  onChange: (value: string) => void;
}) {
  const [visible, setVisible] = useState(false);
  const inputId = useId();
  return (
    <div className="settings-field">
      <label htmlFor={inputId}>
        <span className="settings-field-label">
          <KeyRound size={14} />
          {label}
          <span className={`settings-token-status ${configured ? 'is-configured' : ''}`}>
            {configured ? 'Disponible' : 'À renseigner'}
          </span>
        </span>
      </label>
      <div className="settings-secret-input">
        <input
          id={inputId}
          className="ui-input"
          aria-label={label}
          aria-describedby={`${inputId}-help`}
          type={visible ? 'text' : 'password'}
          autoComplete="new-password"
          spellCheck={false}
          maxLength={4096}
          value={value}
          onChange={(event) => onChange(event.target.value)}
          required={required}
          placeholder={configured ? 'Laisser vide pour conserver la clé' : 'Saisir votre clé API'}
        />
        <button
          type="button"
          aria-label={`${visible ? 'Masquer' : 'Afficher'} : ${label}`}
          aria-pressed={visible}
          onClick={() => setVisible(!visible)}
        >
          {visible ? <EyeOff size={16} /> : <Eye size={16} />}
        </button>
      </div>
      <p id={`${inputId}-help`} className="settings-field-help">
        {configured
          ? 'Saisissez une nouvelle valeur uniquement pour remplacer la clé enregistrée.'
          : 'La clé est nécessaire pour utiliser ce service distant.'}
      </p>
    </div>
  );
}

function OcrSettingsForm({ initial }: { initial: OcrSettings }) {
  const [provider, setProvider] = useState<AvailableProvider>(initial.provider);
  const [model, setModel] = useState(initial.cloudModel);
  const [baseUrl, setBaseUrl] = useState(initial.baseUrl);
  const [token, setToken] = useState('');
  const [autoPaddleProvider, setAutoPaddleProvider] = useState(
    initial.autoPaddleProvider ?? 'LOCAL',
  );
  const [saved, setSaved] = useState(initial);
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState('');
  const [notice, setNotice] = useState('');
  const dirty =
    provider !== saved.provider ||
    model !== saved.cloudModel ||
    baseUrl !== saved.baseUrl ||
    token !== '' ||
    autoPaddleProvider !== (saved.autoPaddleProvider ?? 'LOCAL');
  useUnsavedChanges(dirty);
  const selectedEngine = engines.find((engine) => engine.value === provider)!;
  const local = provider === 'LOCAL' || (provider === 'AUTO' && autoPaddleProvider === 'LOCAL');
  const paddleCloud =
    provider === 'AI_STUDIO' || (provider === 'AUTO' && autoPaddleProvider === 'AI_STUDIO');
  function clearFeedback() {
    setError('');
    setNotice('');
  }
  function restore(settings: OcrSettings) {
    setProvider(settings.provider);
    setModel(settings.cloudModel);
    setBaseUrl(settings.baseUrl);
    setToken('');
    setAutoPaddleProvider(settings.autoPaddleProvider ?? 'LOCAL');
  }
  return (
    <form
      className="settings-form"
      aria-busy={busy}
      onSubmit={async (event) => {
        event.preventDefault();
        if (busy || !dirty) return;
        setBusy(true);
        clearFeedback();
        try {
          const settings = await request<OcrSettings>('/settings/ocr', {
            method: 'PUT',
            body: JSON.stringify({
              provider,
              cloudModel: model,
              baseUrl,
              cloudToken: token.trim() || null,
              autoPaddleProvider,
            }),
          });
          setSaved(settings);
          restore(settings);
          setNotice('Configuration enregistrée. Elle s’applique aux nouvelles conversions.');
        } catch (e) {
          setError((e as Error).message);
        } finally {
          setBusy(false);
        }
      }}
    >
      <section
        className="settings-card settings-engine-section"
        aria-labelledby="settings-engine-title"
      >
        <header className="settings-section-heading">
          <span className="settings-section-number">01</span>
          <div>
            <h2 id="settings-engine-title">Choisir le moteur</h2>
            <p>Sélectionnez le mode de lecture de vos documents.</p>
          </div>
        </header>
        <fieldset className="settings-engine-grid" disabled={busy}>
          <legend className="sr-only">Moteur de lecture</legend>
          {engines.map((engine) => (
            <label
              key={engine.value}
              className={`settings-engine-option ${provider === engine.value ? 'is-selected' : ''}`}
            >
              <input
                type="radio"
                name="ocr-provider"
                value={engine.value}
                checked={provider === engine.value}
                onChange={() => {
                  setProvider(engine.value);
                  clearFeedback();
                }}
              />
              <span className="settings-engine-icon">
                <engine.icon size={22} aria-hidden="true" />
              </span>
              <strong className="settings-engine-name">{engine.name}</strong>
              <span className="settings-engine-copy">{engine.description}</span>
              <small className="settings-engine-tag">{engine.tag}</small>
              <span className="settings-engine-check" aria-hidden="true">
                {provider === engine.value && <Check size={12} />}
              </span>
            </label>
          ))}
        </fieldset>
      </section>
      <div className="settings-layout">
        <div className="settings-main">
          <section className="settings-card" aria-labelledby="settings-connection-title">
            <header className="settings-section-heading">
              <span className="settings-section-number">02</span>
              <div>
                <h2 id="settings-connection-title">Paramètres de connexion</h2>
                <p>{local ? 'PaddleOCR sur votre serveur' : 'PaddleOCR AI Studio'}</p>
              </div>
            </header>
            <fieldset className="settings-fields" disabled={busy}>
              <legend className="sr-only">Connexion au moteur sélectionné</legend>
              {provider === 'AUTO' && (
                <>
                  <p className="settings-inline-note">
                    <Sparkles size={16} />
                    <span>
                      Le texte du PDF est lu en priorité. PaddleOCR prend le relais pour les pages
                      scannées.
                    </span>
                  </p>
                  <label className="settings-field">
                    Service PaddleOCR
                    <select
                      aria-label="Moteur PaddleOCR du mode automatique"
                      className="ui-input"
                      value={autoPaddleProvider}
                      onChange={(event) => {
                        setAutoPaddleProvider(event.target.value as 'LOCAL' | 'AI_STUDIO');
                        clearFeedback();
                      }}
                    >
                      <option value="LOCAL">Local — sans clé API</option>
                      <option value="AI_STUDIO">AI Studio — jeton et quota requis</option>
                    </select>
                  </label>
                </>
              )}
              {local && (
                <label className="settings-field">
                  Adresse du moteur OCR local
                  <input
                    aria-label="Adresse du moteur OCR local"
                    className="ui-input settings-url-input"
                    type="url"
                    required
                    maxLength={2000}
                    value={baseUrl}
                    onChange={(event) => {
                      setBaseUrl(event.target.value);
                      clearFeedback();
                    }}
                    placeholder="http://127.0.0.1:8091"
                    spellCheck={false}
                  />
                  <span className="settings-field-help">
                    Le service PaddleOCR doit être démarré et accessible depuis le serveur de
                    l’application.
                  </span>
                </label>
              )}
              {paddleCloud && (
                <>
                  <p className="settings-inline-note">
                    <Cloud size={16} />
                    <span>
                      Lecture via AI Studio : un jeton valide et du quota sont nécessaires.
                    </span>
                  </p>
                  <div className="settings-connection-grid">
                    <label className="settings-field">
                      Modèle PaddleOCR
                      <select
                        aria-label="Modèle PaddleOCR"
                        className="ui-input"
                        value={model}
                        onChange={(event) => {
                          setModel(event.target.value);
                          clearFeedback();
                        }}
                      >
                        <option value="PP-OCRv6">PP-OCRv6 — texte et positions</option>
                        <option value="PaddleOCR-VL-1.6">
                          PaddleOCR-VL-1.6 — tableaux et mise en page
                        </option>
                      </select>
                    </label>
                    <SecretField
                      label="Jeton API PaddleOCR"
                      value={token}
                      configured={saved.hasToken}
                      required={!saved.hasToken}
                      onChange={(value) => {
                        setToken(value);
                        clearFeedback();
                      }}
                    />
                  </div>
                  <details className="settings-service-details">
                    <summary>
                      Adresse du service et accès au jeton
                      <ChevronDown size={14} />
                    </summary>
                    <p>{saved.apiUrl}</p>
                    <a href="https://aistudio.baidu.com/paddleocr" target="_blank" rel="noreferrer">
                      Ouvrir PaddleOCR AI Studio ↗
                    </a>
                  </details>
                </>
              )}
            </fieldset>
            <div className="settings-save-area">
              {error && (
                <p role="alert" className="settings-feedback is-error">
                  <AlertCircle size={17} />
                  {error}
                </p>
              )}
              {notice && (
                <p role="status" className="settings-feedback is-success">
                  <CheckCircle2 size={17} />
                  {notice}
                </p>
              )}
              <footer className="settings-actions">
                <div className="settings-action-status">
                  {dirty ? <span className="settings-unsaved-dot" /> : <CheckCircle2 size={16} />}
                  <p>
                    {dirty ? 'Modifications à enregistrer' : 'Paramètres à jour'}
                    <small>Les conversions en cours conservent leurs réglages.</small>
                  </p>
                </div>
                <div className="settings-action-buttons">
                  <Button
                    type="button"
                    variant="outline"
                    disabled={busy || !dirty}
                    onClick={() => {
                      restore(saved);
                      clearFeedback();
                    }}
                  >
                    <RotateCcw size={14} />
                    Rétablir
                  </Button>
                  <Button type="submit" disabled={busy || !dirty}>
                    {busy ? (
                      <LoaderCircle size={15} className="animate-spin" />
                    ) : (
                      <Save size={15} />
                    )}
                    {busy ? 'Enregistrement…' : 'Enregistrer'}
                  </Button>
                </div>
              </footer>
            </div>
          </section>
        </div>
        <aside className="settings-sidebar" aria-label="Informations sur la configuration">
          <section className="settings-card settings-summary">
            <div className="settings-summary-visual" key={provider}>
              <span className="settings-preview-label">VOTRE CONFIGURATION</span>
              <span className="settings-preview-icon">
                <selectedEngine.icon size={26} />
              </span>
              <h2>{selectedEngine.name}</h2>
              <p>{local ? 'Lecture sur votre serveur' : 'Lecture avec AI Studio'}</p>
              <div className="settings-reading-flow" aria-label="Du PDF aux données structurées">
                <FileText size={17} />
                <ArrowRight size={13} />
                <ScanLine size={17} />
                <ArrowRight size={13} />
                <Table2 size={17} />
              </div>
            </div>
            <div className="settings-summary-body">
              <dl>
                <div>
                  <dt>Service OCR</dt>
                  <dd>{local ? 'PaddleOCR local' : 'PaddleOCR AI Studio'}</dd>
                </div>
                <div>
                  <dt>Accès au service</dt>
                  <dd>
                    {local
                      ? 'Sans clé API'
                      : token.trim()
                        ? 'Nouveau jeton saisi'
                        : saved.hasToken
                          ? 'Jeton enregistré'
                          : 'Jeton à renseigner'}
                  </dd>
                </div>
                <div>
                  <dt>Application des réglages</dt>
                  <dd>Prochaines conversions</dd>
                </div>
              </dl>
              <p role="status" className={`settings-save-state ${dirty ? 'is-dirty' : ''}`}>
                <span aria-hidden="true" />
                {dirty ? 'Modifications non enregistrées' : 'Configuration enregistrée'}
              </p>
              {dirty && (
                <p className="settings-summary-help">
                  Enregistrez pour appliquer cette configuration.
                </p>
              )}
            </div>
          </section>
          <section className="settings-security">
            <LockKeyhole size={19} />
            <div>
              <h2>Clés protégées</h2>
              <p>
                Les clés sont chiffrées côté serveur. Les valeurs enregistrées ne sont jamais
                renvoyées au navigateur.
              </p>
            </div>
          </section>
          <p className="settings-sidebar-note">
            <ShieldCheck size={16} />
            <span>
              Les contrôles des dates, montants et soldes restent actifs quel que soit le moteur
              choisi.
            </span>
          </p>
        </aside>
      </div>
    </form>
  );
}
