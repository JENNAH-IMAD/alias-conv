import Link from 'next/link';
import { LayoutDashboard } from 'lucide-react';
import { PageHeader } from '@/components/page-header';
import { Button } from '@/components/ui/button';
export default function LoginPage() {
  return (
    <div className="ui-page">
      <PageHeader
        eyebrow="Espace de travail"
        title="Vous êtes connecté"
        description="Votre espace de travail est disponible."
        icon={LayoutDashboard}
        actions={
          <Button asChild>
            <Link href="/dashboard">Ouvrir mon espace</Link>
          </Button>
        }
      />
    </div>
  );
}
