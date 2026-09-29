'use client';

import Link from 'next/link';
import { Users, Landmark, CheckCheck, CircleAlert, ArrowUpRight, FileUp, ScanLine, FileOutput, ArrowRight, FileText, RefreshCw } from 'lucide-react';
import { Dashboard } from '@/services/api';
import { useApi } from '@/services/use-api';
import { useAuth } from '@/components/auth-context';
import { Avatar } from '@/components/avatar';
import { Button } from '@/components/ui/button';
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/ui/card';
import { Badge } from '@/components/ui/badge';
import { Skeleton } from '@/components/ui/skeleton';
import { Alert, AlertDescription, AlertTitle } from '@/components/ui/alert';
import { StatusBadge } from '@/components/ui/status-badge';

export default function DashboardPage() {
  const { data, error, loading, reload } = useApi<Dashboard>('/dashboard');
  const { user } = useAuth();
  const metrics = [
    { label: 'Clients', value: data?.clients, icon: Users, href: '/clients', note: 'Votre portefeuille clients', color: 'text-[var(--link)] bg-primary/10' },
    { label: 'Banques', value: data?.banks, icon: Landmark, href: '/banks', note: 'Établissements référencés', color: 'text-[var(--link)] bg-primary/10' },
    { label: 'Conversions réussies', value: data?.completed, icon: CheckCheck, href: '/conversions/history?status=Completed', note: 'Relevés traités avec succès', color: 'text-[var(--success-text)] bg-success/10' },
    { label: 'Conversions échouées', value: data?.failed, icon: CircleAlert, href: '/conversions/history?status=Failed', note: 'Traitements à vérifier', color: 'text-destructive bg-destructive/10' },
  ];

  return <div className="ui-page">
    <section className="ui-hero flex flex-wrap items-center justify-between gap-5"><div><p className="ui-eyebrow">VOTRE ESPACE DE TRAVAIL</p><h1 className="ui-title">Bonjour, {user?.name?.split(' ')[0] || 'bienvenue'}<span className="accent-text">.</span></h1><p className="ui-description">Vos relevés, vos clients et vos conversions. Tout est ici.</p></div><Button asChild size="lg"><Link href="/conversions"><FileUp/> Importer un relevé</Link></Button></section>
    {error && <Alert variant="destructive"><CircleAlert/><AlertTitle>Impossible de charger le tableau de bord</AlertTitle><AlertDescription><p>{error}</p><Button variant="outline" size="sm" onClick={reload}><RefreshCw/> Réessayer</Button></AlertDescription></Alert>}
    <section aria-label="Indicateurs de votre activité" aria-busy={loading} className="grid gap-4 sm:grid-cols-2 xl:grid-cols-4">
      {metrics.map(({ label, value, icon: Icon, href, note, color }) => <Link href={href} key={label} className="group rounded-xl focus-visible:outline-offset-4"><Card className="h-full gap-0 transition-colors group-hover:border-primary/40"><CardContent><div className="flex items-center justify-between"><span className={`rounded-xl p-2.5 ${color}`}><Icon className="size-5"/></span><ArrowUpRight className="size-4 text-muted-foreground transition-transform group-hover:-translate-y-0.5 group-hover:translate-x-0.5"/></div>{loading ? <Skeleton className="my-4 h-10 w-20"/> : <p className="mt-4 text-4xl font-semibold tracking-tight tabular-nums">{value?.toLocaleString('fr-FR') ?? '—'}</p>}<h2 className="mt-2 text-sm font-medium">{label}</h2><p className="mt-2 text-xs text-muted-foreground">{note}</p></CardContent></Card></Link>)}
    </section>
    <section className="grid items-start gap-6 xl:grid-cols-[minmax(0,1fr)_320px]">
      <Card className="min-w-0 overflow-hidden"><CardHeader className="flex flex-wrap items-center justify-between gap-3 border-b border-border pb-5"><div><CardTitle>Conversions récentes</CardTitle><CardDescription className="mt-2">L’activité de vos derniers relevés bancaires.</CardDescription></div><Button asChild variant="ghost" size="sm"><Link href="/conversions/history">Tout voir <ArrowRight/></Link></Button></CardHeader><CardContent>
        {loading ? <div className="space-y-5" role="status"><span className="sr-only">Chargement des conversions</span>{[0, 1, 2].map(i => <div key={i} className="flex items-center gap-3"><Skeleton className="size-10 rounded-full"/><div className="flex-1 space-y-2"><Skeleton className="h-4 w-1/2"/><Skeleton className="h-3 w-2/3"/></div><Skeleton className="h-6 w-20"/></div>)}</div> : !error && !data?.recent.length ? <div className="flex flex-col items-center py-12 text-center"><span className="rounded-2xl bg-muted p-4 text-muted-foreground"><FileText className="size-8" strokeWidth={1.5}/></span><h3 className="mt-5 font-semibold">Votre premier relevé vous attend</h3><p className="mt-2 max-w-sm text-sm leading-6 text-muted-foreground">Importez un PDF pour retrouver ici vos conversions et suivre leur avancement.</p><Button asChild variant="outline" className="mt-5"><Link href="/conversions"><FileUp/> Importer un relevé</Link></Button></div> : data?.recent.map(item => <Link key={item.id} href="/conversions/history" className="flex flex-wrap items-center justify-between gap-3 border-b border-border py-4 first:pt-0 last:border-0 last:pb-0"><div className="flex min-w-0 flex-1 items-center gap-3"><Avatar name={item.client} small/><div className="min-w-0"><p className="truncate text-sm font-medium">{item.client}</p><p className="mt-1 truncate text-xs text-muted-foreground">{item.bank} · {item.accountName || "Compte non renseigné"} · {new Date(item.createdAt).toLocaleDateString('fr-FR')}</p><p className="mt-1 text-xs text-muted-foreground">Traité par : {item.processedByName || 'Non renseigné'} · Modifié par : {item.modifiedByName || 'Non renseigné'}</p></div></div><div className="shrink-0 text-right"><StatusBadge status={item.status}/><p className="mt-2 text-xs text-muted-foreground">{item.count} opérations</p></div></Link>)}
      </CardContent></Card>
      <Card className="workflow-card"><CardHeader><Badge variant="secondary" className="w-fit">LE PARCOURS</Badge><CardTitle className="mt-2 text-xl">Du PDF à votre export</CardTitle><CardDescription>Un traitement guidé, étape par étape.</CardDescription></CardHeader><CardContent><ol className="space-y-6">{[{ icon: FileUp, title: 'Importer le relevé', text: 'Associez votre PDF à un client et à son compte.' }, { icon: ScanLine, title: 'Vérifier les opérations', text: 'Contrôlez les données extraites du document.' }, { icon: FileOutput, title: 'Exporter les écritures', text: 'Téléchargez votre fichier comptable.' }].map(({ icon: Icon, title, text }, index) => <li key={title} className="flex items-start gap-3"><span className="flex size-9 shrink-0 items-center justify-center rounded-lg border border-border bg-card text-primary"><Icon className="size-4"/></span><div><h3 className="text-sm font-medium"><span className="mr-2 text-muted-foreground">0{index + 1}</span>{title}</h3><p className="mt-1.5 text-xs leading-5 text-muted-foreground">{text}</p></div></li>)}</ol><Button asChild className="mt-7 w-full"><Link href="/conversions">Nouvelle conversion <ArrowRight/></Link></Button></CardContent></Card>
    </section>
  </div>;
}
