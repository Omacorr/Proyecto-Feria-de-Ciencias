// Genera el video promocional a partir de index.html.
//   node render.js               -> video completo (salida/recuerdos-rotos-promo.mp4)
//   node render.js --fotos 2,7   -> solo capturas de esos segundos (salida/foto-<seg>.jpg), sin ffmpeg
// Usa Chrome sin ventana (protocolo DevTools) para dibujar cada frame y ffmpeg
// para armar el video y mezclar el sonido. No necesita npm install.
const http = require('http'), fs = require('fs'), path = require('path');
const { spawn, spawnSync } = require('child_process');

const DIR = __dirname, OUT = path.join(DIR, 'salida');
const CHROME = ['C:/Program Files/Google/Chrome/Application/chrome.exe', 'C:/Program Files (x86)/Microsoft/Edge/Application/msedge.exe'].find(fs.existsSync);
const AUD = path.join(DIR, '..', '..', 'Assets', 'Audios');
const args = process.argv.slice(2);
const fotos = args.includes('--fotos') ? args[args.indexOf('--fotos') + 1].split(',').map(Number) : null;

function findFfmpeg() {
  if (spawnSync('ffmpeg', ['-version']).status === 0) return 'ffmpeg';
  const links = path.join(process.env.LOCALAPPDATA || '', 'Microsoft', 'WinGet', 'Links', 'ffmpeg.exe');
  if (fs.existsSync(links)) return links;
  const pk = path.join(process.env.LOCALAPPDATA || '', 'Microsoft', 'WinGet', 'Packages');
  if (fs.existsSync(pk)) for (const d of fs.readdirSync(pk)) if (/ffmpeg/i.test(d)) {
    const stack = [path.join(pk, d)];
    while (stack.length) { const p = stack.pop(); for (const e of fs.readdirSync(p, { withFileTypes: true })) {
      const f = path.join(p, e.name); if (e.isDirectory()) stack.push(f); else if (e.name.toLowerCase() === 'ffmpeg.exe') return f; } }
  }
  return null;
}

// Sonidos del propio juego: [archivo, segundo en que entra, volumen, duración máxima (0 = entero)]
const SONIDOS = [
  ['Efectos/latidos_despertar.wav', 0.2, 1.0, 4.6],
  ['Efectos/zumbido_grave.wav', 0, 0.55, 14],
  ['Efectos/golpe_seco.wav', 6.3, 0.9, 0],
  ['Efectos/golpe_seco.wav', 10.6, 0.8, 0],
  ['Efectos/crujido_madera_02.wav', 10.5, 0.7, 0],
  ['Efectos/zumbido_grave.wav', 14, 0.45, 20],
  ['Efectos/clic_linterna.wav', 14.2, 0.9, 0],
  ['Parte2/cumple_cajita_distorsionada.wav', 18.1, 0.6, 3.8],
  ['Efectos/golpes_madera.wav', 22.4, 0.7, 3.4],
  ['Efectos/crujido_madera_01.wav', 26.3, 0.8, 0],
  ['Efectos/zumbido_fluorescente.wav', 30.1, 0.5, 3.8],
  ['Efectos/zumbido_grave.wav', 34, 0.4, 6],
  ['MenuSelect.wav', 36.7, 0.9, 0],
  ['Parte2/monstruo_respiracion.wav', 40.2, 0.9, 4.6],
  ['Parte2/monstruo_pasos.wav', 40.6, 0.7, 4.2],
  ['Efectos/golpe_seco.wav', 41.3, 0.8, 0],
  ['Efectos/monitor_bip.wav', 45.3, 0.7, 6.4],
  ['Efectos/zumbido_grave.wav', 45, 0.3, 7],
];

const MIME = { '.html': 'text/html', '.ttf': 'font/ttf', '.jpg': 'image/jpeg', '.png': 'image/png', '.js': 'text/javascript' };
const server = http.createServer((req, res) => {
  const f = path.join(DIR, decodeURIComponent(req.url.split('?')[0]).replace(/^\/+/, '') || 'index.html');
  if (!f.startsWith(DIR) || !fs.existsSync(f) || fs.statSync(f).isDirectory()) { res.writeHead(404); return res.end(); }
  res.writeHead(200, { 'content-type': MIME[path.extname(f)] || 'application/octet-stream' }); fs.createReadStream(f).pipe(res);
});

(async () => {
  fs.mkdirSync(OUT, { recursive: true });
  await new Promise(r => server.listen(0, '127.0.0.1', r));
  const url = `http://127.0.0.1:${server.address().port}/index.html?render`;
  const prof = fs.mkdtempSync(path.join(require('os').tmpdir(), 'promo-chrome-'));
  const chrome = spawn(CHROME, ['--headless=new', '--remote-debugging-port=9377', `--user-data-dir=${prof}`, '--window-size=1080,1920',
    '--hide-scrollbars', '--mute-audio', '--disable-gpu', 'about:blank'], { stdio: 'ignore' });
  let ws;
  for (let i = 0; i < 60 && !ws; i++) {
    await new Promise(r => setTimeout(r, 250));
    try { const list = await (await fetch('http://127.0.0.1:9377/json')).json(); const pg = list.find(p => p.type === 'page'); if (pg) ws = new WebSocket(pg.webSocketDebuggerUrl); } catch (e) {}
  }
  if (!ws) throw new Error('No pude conectar con Chrome');
  await new Promise(r => ws.addEventListener('open', r));
  let id = 0; const pending = new Map();
  ws.addEventListener('message', ev => { const m = JSON.parse(ev.data); if (m.id && pending.has(m.id)) { pending.get(m.id)(m); pending.delete(m.id); } });
  const cdp = (method, params = {}) => new Promise(res => { const n = ++id; pending.set(n, res); ws.send(JSON.stringify({ id: n, method, params })); });
  const evalJs = async expr => { const r = await cdp('Runtime.evaluate', { expression: expr, returnByValue: true }); if (r.result.exceptionDetails) throw new Error(JSON.stringify(r.result.exceptionDetails)); return r.result.result.value; };

  await cdp('Page.navigate', { url });
  for (let i = 0; i < 100; i++) { await new Promise(r => setTimeout(r, 200)); if (await evalJs('window.ready === true').catch(() => false)) break; }
  const DUR = await evalJs('DUR'), FPS = await evalJs('FPS');
  const frame = async t => Buffer.from((await evalJs(`(drawAt(${t}), document.getElementById('c').toDataURL('image/jpeg', 0.95))`)).split(',')[1], 'base64');

  if (fotos) {
    for (const t of fotos) { fs.writeFileSync(path.join(OUT, `foto-${t}.jpg`), await frame(t)); console.log('foto', t); }
  } else {
    const FF = findFfmpeg(); if (!FF) throw new Error('No encuentro ffmpeg');
    const mudo = path.join(OUT, 'sin-sonido.mp4'), final = path.join(OUT, 'recuerdos-rotos-promo.mp4');
    const ff = spawn(FF, ['-y', '-f', 'image2pipe', '-framerate', String(FPS), '-c:v', 'mjpeg', '-i', '-', '-c:v', 'libx264', '-preset', 'slow', '-crf', '17',
      '-pix_fmt', 'yuv420p', '-movflags', '+faststart', mudo], { stdio: ['pipe', 'ignore', 'inherit'] });
    const total = Math.round(DUR * FPS);
    for (let f = 0; f < total; f++) {
      if (!ff.stdin.write(await frame(f / FPS))) await new Promise(r => ff.stdin.once('drain', r));
      if (f % 150 === 0) console.log(`frame ${f}/${total}`);
    }
    ff.stdin.end(); await new Promise(r => ff.on('close', r));

    // mezcla de sonido
    const ins = [], filt = [], names = [];
    SONIDOS.forEach(([file, at, vol, max], i) => {
      const p = path.join(AUD, file); if (!fs.existsSync(p)) { console.log('falta sonido', file); return; }
      const k = ins.length / 2 + 1; ins.push('-i', p);
      const trim = max > 0 ? `atrim=0:${max},afade=t=out:st=${Math.max(0, max - 0.6)}:d=0.6,` : '';
      filt.push(`[${k}:a]aformat=sample_rates=48000:channel_layouts=stereo,${trim}afade=t=in:d=0.05,volume=${vol},adelay=${Math.round(at * 1000)}:all=1[a${i}]`);
      names.push(`[a${i}]`);
    });
    filt.push(`${names.join('')}amix=inputs=${names.length}:normalize=0:duration=longest,alimiter=limit=0.9,afade=t=out:st=${DUR - 1}:d=1[mix]`);
    const r = spawnSync(FF, ['-y', '-i', mudo, ...ins, '-filter_complex', filt.join(';'), '-map', '0:v', '-map', '[mix]', '-c:v', 'copy', '-c:a', 'aac', '-b:a', '192k',
      '-t', String(DUR), '-movflags', '+faststart', final], { stdio: ['ignore', 'ignore', 'inherit'] });
    console.log(r.status === 0 ? 'LISTO ' + final : 'ERROR en la mezcla de sonido');
  }
  ws.close(); chrome.kill(); server.close();
  try { fs.rmSync(prof, { recursive: true, force: true }); } catch (e) {}
})().catch(e => { console.error(e); process.exit(1); });
