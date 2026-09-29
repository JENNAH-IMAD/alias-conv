'use client';
import { can } from '@/lib/permissions';
import { Button } from '@/components/ui/button';
import { Avatar } from '@/components/avatar';
import { pageTheme } from '@/components/page-theme';

import { useTheme } from '@/components/theme-context';
import { useState } from 'react';
import { useAuth } from '@/components/auth-context';
import { ResourceEditor, LoadState, Pagination } from '@/components/resource-editor';
import { Statement, Page, request } from '@/services/api';
import { useApi } from '@/services/use-api';

function getStatusStyles(status: string) {
  if (status === 'Actif') {
    return {
      badge: 'status-positive',
      dot: 'bg-success',
      label: 'Actif',
    };
  }

  return {
    badge: 'ui-tag',
    dot: 'bg-warning',
    label: 'Inactif',
  };
}

export default function ExtractionProfilesPage() {
  const { isLightMode } = useTheme();
  const { user } = useAuth(); const canEdit = can(user, 'profiles.write');
  const [page,setPage] = useState(1);
  const {data,error,loading,reload} = useApi<Page<Statement>>('/bank-statement-templates?page='+page+'&pageSize=20');
  const extractionProfiles = data?.items || [];
  const [editing,setEditing] = useState<Statement | null | undefined>(undefined);
  const [actionError,setActionError] = useState('');
  const remove = async (item:Statement) => {if(!confirm('Supprimer cet élément ?'))return;try{await request('/bank-statement-templates/'+item.id,{method:'DELETE'});reload();}catch(e){setActionError((e as Error).message);}};
  const theme = pageTheme;

  return (
    <div className={theme.page}>
      <LoadState error={error||actionError} loading={loading} empty={!extractionProfiles.length}/>
      {editing !== undefined && <ResourceEditor resource="bank-statement-templates" initial={editing||undefined} onClose={()=>setEditing(undefined)} onSaved={reload}/>}
      <section className={theme.hero}>
        <div className="flex flex-col gap-5 md:flex-row md:items-center md:justify-between">
          <div>
            <p className={theme.chip}>Moteur d’extraction</p>
            <h1 className={theme.title}>Profils d’extraction</h1>
          </div>
          {canEdit && <Button className={theme.button} onClick={()=>setEditing(null)}>Nouveau profil</Button>}
        </div>


      </section>

      <section className="grid gap-5 xl:grid-cols-2">
        {extractionProfiles.map((profile) => {
          const statusStyles = getStatusStyles(profile.isActive?'Actif':'Inactif');
          return (
            <article key={profile.id} className={theme.card} aria-label={`Profil d'extraction ${profile.name}`}>
              <div className={`flex items-start justify-between gap-4 border-b ${isLightMode ? 'border-border' : 'border-border'} pb-4`}>
                <div className="flex min-w-0 items-start gap-3">
                  <Avatar name={profile.bank}/>
                  <div className="min-w-0 break-words">
                    <h2 className={isLightMode ? 'text-lg font-semibold tracking-tight text-foreground' : 'text-lg font-semibold tracking-tight text-foreground'}>{profile.name}</h2>
                    <p className={isLightMode ? 'mt-1 text-sm text-muted-foreground' : 'mt-1 text-sm text-muted-foreground'}>{profile.bank}</p>
                  </div>
                </div>
                <span className={`inline-flex items-center gap-2 rounded-full px-2.5 py-1.5 text-xs font-semibold ${statusStyles.badge}`}><span className={`h-2 w-2 rounded-full ${statusStyles.dot}`} aria-hidden="true" />{statusStyles.label}</span>
              </div>

              <div className="mt-4 grid gap-3 sm:grid-cols-3">
                <div className="ui-summary space-y-2"><p className={theme.itemLabel}>Détection</p><p className={theme.itemValue}>{profile.parserKey==='multibank-auto'?'Automatique multibanque':'Lignes délimitées'}</p></div>
                <div className="ui-summary space-y-2"><p className={theme.itemLabel}>Colonnes</p><p className={theme.itemValue}>{profile.parserKey==='multibank-auto'?'Détection automatique':'Indices configurables'}</p></div>
                <div className="ui-summary space-y-2"><p className={theme.itemLabel}>Format de date</p><p className={theme.itemValue}>{profile.parserKey==='multibank-auto'?'Automatique':profile.dateFormat}</p></div>
              </div>

              <div className="mt-5">
                <div className="mb-2 flex items-center justify-between">
                  <p className={theme.itemLabel}>Règles actives</p>
                  
                </div>
                <div className="flex flex-wrap gap-2">
                  {(profile.parserKey==='multibank-auto'?['PDF texte','Soldes vérifiés','Reports vérifiés']:[profile.delimiter === '\t' ? 'Tabulation' : profile.delimiter, profile.numberCulture, profile.description]).filter(Boolean).map((item) => <span key={item} className={theme.tag}>{item}</span>)}
                </div>
              </div>


            {canEdit && <div className="mt-4 flex gap-4 text-sm"><Button variant="outline" onClick={()=>setEditing(profile)}>Modifier</Button><Button variant="outline" className="danger-button" onClick={()=>remove(profile)}>Supprimer</Button></div>}</article>
          );
        })}
      </section><Pagination page={page} total={data?.total||0} onChange={setPage}/>
    </div>
  );
}
