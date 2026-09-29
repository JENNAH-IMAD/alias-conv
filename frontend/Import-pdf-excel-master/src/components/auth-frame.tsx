'use client';
import { Layers, FileText, ArrowRight, Table2, ShieldCheck } from 'lucide-react';
import { ThemeSwitch } from './theme-switch';
import { BrandLogo } from './brand-logo';
export function AuthFrame({children}:{children:React.ReactNode}){

 return <div className="auth-layout"><section className="auth-story"><div className="brand"><BrandLogo/></div><div className="auth-story-content"><p className="ui-eyebrow">MOINS DE SAISIE. PLUS DE CLARTÉ.</p><h1>Vos relevés bancaires.<br/><span>Un travail simplifié.</span></h1><p>Centralisez vos clients, traitez vos relevés PDF et préparez vos exports comptables dans un seul espace.</p><div className="auth-flow"><span><FileText size={30}/><small>Relevé PDF</small></span><ArrowRight size={20}/><span><Layers size={30}/><small>Alias Convert PDF</small></span><ArrowRight size={20}/><span><Table2 size={30}/><small>Export comptable</small></span></div><div className="flex items-center gap-2 text-xs"><ShieldCheck size={17}/> Un espace dédié à votre activité.</div></div><p className="text-xs opacity-60">Alias Convert PDF · Gestion des relevés bancaires</p></section><section className="auth-form-section"><div className="auth-theme"><ThemeSwitch/></div><div className="auth-form-content"><div className="auth-mobile-brand"><BrandLogo/></div>{children}</div><p className="muted text-center text-xs">Votre espace de travail, à portée de main.</p></section></div>;
}
