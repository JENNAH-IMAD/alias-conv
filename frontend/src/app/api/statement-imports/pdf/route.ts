import { request as httpRequest } from 'node:http';
import { request as httpsRequest } from 'node:https';
import { Readable } from 'node:stream';

export const runtime = 'nodejs';

export async function POST(request: Request) {
  // Long conversions use a streaming relay without the rewrite proxy's response timer.
  // Authentication, upload size and conversion admission remain enforced by the API.
  const base = (process.env.API_PROXY_URL || 'http://127.0.0.1:5081').replace(/\/$/, '');
  const target = new URL(`${base}/api/statement-imports/pdf`);
  const headers = Object.fromEntries(request.headers);
  for (const name of ['host', 'connection', 'transfer-encoding', 'content-length'])
    delete headers[name];
  try {
    const upstream = await new Promise<import('node:http').IncomingMessage>((resolve, reject) => {
      const send = target.protocol === 'https:' ? httpsRequest : httpRequest;
      const outgoing = send(
        target,
        { method: 'POST', headers, signal: request.signal, timeout: 0 },
        resolve,
      );
      outgoing.once('error', reject);
      if (request.body) {
        const body = Readable.fromWeb(request.body as import('node:stream/web').ReadableStream);
        body.once('error', (error) => outgoing.destroy(error));
        outgoing.once('close', () => body.destroy());
        body.pipe(outgoing);
      } else outgoing.end();
    });
    const responseHeaders = new Headers();
    for (const [name, value] of Object.entries(upstream.headers)) {
      if (value === undefined || ['connection', 'transfer-encoding', 'keep-alive'].includes(name))
        continue;
      for (const entry of Array.isArray(value) ? value : [value])
        responseHeaders.append(name, entry);
    }
    return new Response(Readable.toWeb(upstream) as ReadableStream<Uint8Array>, {
      status: upstream.statusCode || 502,
      headers: responseHeaders,
    });
  } catch {
    return Response.json(
      {
        title: request.signal.aborted
          ? 'Import annulé.'
          : 'Le serveur de conversion est inaccessible.',
      },
      { status: request.signal.aborted ? 499 : 502 },
    );
  }
}
