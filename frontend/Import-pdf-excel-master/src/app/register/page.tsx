'use client';
import { Input } from '@/components/ui/input';
import { Button } from '@/components/ui/button';
import Link from 'next/link';
import { FormEvent, useState } from 'react';
import { useRouter } from 'next/navigation';
import { ArrowRight } from 'lucide-react';
import { request } from '@/services/api';
import { AuthFrame } from '@/components/auth-frame';
export default function RegisterPage(){
 const router=useRouter();const [error,setError]=useState('');const [busy,setBusy]=useState(false);
 const submit=async(event:FormEvent<HTMLFormElement>)=>{event.preventDefault();if(busy)return;const data=new FormData(event.currentTarget);setBusy(true);setError('');try{await request('/auth/register',{method:'POST',body:JSON.stringify({username:String(data.get('firstName')).trim()+' '+String(data.get('lastName')).trim(),email:data.get('email'),password:data.get('password')})});router.push('/login');}catch(e){setError((e as Error).message);}finally{setBusy(false);}};
 return <AuthFrame><p className="ui-eyebrow">BIENVENUE SUR ALIAS CONVERT PDF</p><h2 className="ui-title mt-3">Créez votre espace.</h2><p className="ui-description mb-7">Commencez à organiser vos relevés bancaires.</p><form onSubmit={submit} className="space-y-5"><fieldset disabled={busy} className="space-y-5"><div className="grid gap-4 sm:grid-cols-2"><label><span className="ui-label">Prénom</span><Input name="firstName" autoComplete="given-name" required maxLength={80}/></label><label><span className="ui-label">Nom</span><Input name="lastName" autoComplete="family-name" required maxLength={80}/></label></div><label className="block"><span className="ui-label">Adresse e-mail</span><Input name="email" type="email" autoComplete="email" required placeholder="vous@entreprise.com"/></label><label className="block"><span className="ui-label">Mot de passe</span><Input name="password" type="password" autoComplete="new-password" required minLength={12} maxLength={128}/><span className="muted mt-2 block text-xs">12 à 128 caractères, avec majuscule, minuscule et chiffre.</span></label></fieldset>{error&&<p role="alert" className="rounded-lg bg-destructive/10 p-3 text-sm text-destructive">{error}</p>}<Button className="w-full" disabled={busy}>{busy?'Création en cours…':'Créer mon compte'}<ArrowRight size={17}/></Button></form><p className="muted mt-7 text-center text-xs">Vous avez déjà un compte ? <Link className="accent-text font-semibold" href="/login">Se connecter</Link></p></AuthFrame>;
}
