// Measures HTTP responses, not browser rendering, API conversion or ICMP ping.
const base = new URL(process.argv[2] || 'http://localhost:3002');
if (!['http:', 'https:'].includes(base.protocol)) {
  throw new Error('Utilisez une URL http ou https.');
}
console.log(`Temps HTTP : ${base.origin} (premier appel puis 5 appels repetes)`);
for (const route of ['/', '/login']) {
  const samples = [];
  try {
    for (let index = 0; index < 6; index += 1) {
      const start = performance.now();
      const response = await fetch(new URL(route, base), {
        redirect: 'manual',
        signal: AbortSignal.timeout(30_000),
      });
      await response.arrayBuffer();
      if (response.status >= 400) throw new Error(`HTTP ${response.status}`);
      samples.push({ ms: Math.round(performance.now() - start), status: response.status });
    }
    const repeated = samples.slice(1).map((sample) => sample.ms).sort((a, b) => a - b);
    console.log(`${route} : HTTP ${samples[0].status}, premier ${samples[0].ms} ms, mediane ${repeated[2]} ms, min ${repeated[0]} ms, max ${repeated[4]} ms`);
  } catch (error) {
    console.error(`${route} : ${error.message}. Verifiez que le serveur est demarre.`);
    process.exitCode = 1;
  }
}
