import type { CSSProperties } from 'react';
export function Avatar({name,small=false}:{name:string;small?:boolean}) {
 const words=name.trim().split(/\s+/); const initials=(words.length>1?words[0][0]+words[words.length-1][0]:name.slice(0,2)).toUpperCase();
 const hues=[200,205,210,215,220]; const index=Array.from(name).reduce((n,c)=>n+c.charCodeAt(0),0)%hues.length;
 return <span className={`profile-avatar ${small?'avatar-small':''}`} style={{'--avatar-hue':hues[index]} as CSSProperties} aria-hidden="true">{initials||'CL'}</span>;
}
