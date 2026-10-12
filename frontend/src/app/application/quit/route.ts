import { spawn } from 'node:child_process';

export const runtime = 'nodejs';

export async function POST(request: Request) {
  // Only the application itself may ask its Windows launcher to stop.
  if (request.headers.get('origin') !== new URL(request.url).origin)
    return Response.json({ title: 'Origine de la demande invalide.' }, { status: 403 });
  const launcher = process.env.ALIAS_DESKTOP_LAUNCHER;
  if (!launcher) return Response.json({ desktop: false });
  try {
    const child = spawn(launcher, ['--stop', '--from-ui'], {
      windowsHide: true,
      detached: true,
      stdio: 'ignore',
    });
    await new Promise<void>((resolve, reject) => {
      child.once('spawn', resolve);
      child.once('error', reject);
    });
    child.unref();
    return Response.json({ desktop: true });
  } catch {
    return Response.json(
      { title: 'Impossible de quitter. Utilisez le menu Alias près de l’horloge Windows.' },
      { status: 500 },
    );
  }
}
