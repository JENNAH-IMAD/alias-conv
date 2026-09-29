'use client';
import { createContext, useCallback, useContext, useEffect, useState } from 'react';
import { getSession, request, Session, User } from '@/services/api';
interface AuthContextValue { isAuthenticated:boolean; user:User|null; isReady:boolean; login:(email:string,password:string)=>Promise<void>; logout:()=>void; }
const AuthContext = createContext<AuthContextValue | undefined>(undefined);
export function AuthProvider({children}:{children:React.ReactNode}) {
  const [user,setUser] = useState<User|null>(null); const [isReady,setReady] = useState(false);
  const logout = useCallback(()=> {sessionStorage.removeItem('bank-converter-session');setUser(null);},[]);
  useEffect(()=> {
    queueMicrotask(()=> {localStorage.removeItem('bank-converter-auth');setUser(getSession()?.user || null);setReady(true);});
    window.addEventListener('session-expired',logout);
    const timer = setInterval(()=> {if(!getSession()) logout();},15000);
    return ()=> {window.removeEventListener('session-expired',logout);clearInterval(timer);};
  },[logout]);
  const login = async (email:string,password:string) => {const session = await request<Session>('/auth/login',{method:'POST',body:JSON.stringify({email,password})});sessionStorage.setItem('bank-converter-session',JSON.stringify(session));setUser(session.user);};
  return <AuthContext.Provider value={{isAuthenticated:Boolean(user),user,isReady,login,logout}}>{children}</AuthContext.Provider>;
}
export function useAuth() {const context=useContext(AuthContext);if(!context)throw new Error('AuthProvider requis');return context;}
