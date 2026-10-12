import Link from 'next/link';
export default function LoginPage() {
  return (
    <section className="ui-card">
      <h1 className="ui-title">Vous êtes connecté</h1>
      <p className="muted mt-3">Votre espace de travail est disponible.</p>
      <Link className="ui-button mt-5" href="/dashboard">
        Ouvrir mon espace
      </Link>
    </section>
  );
}
