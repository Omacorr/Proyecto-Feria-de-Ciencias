#!/usr/bin/env node
// Servidor del operador para la feria. Node puro, sin dependencias.
// Uso: node server.js [--port 8080] [--simular] [--adb <ruta>]
'use strict';
const http = require('http');
const net = require('net');
const fs = require('fs');
const path = require('path');
const { spawn, execFile } = require('child_process');

// ---------- argumentos ----------
const argv = process.argv.slice(2);
const getArg = (n, d) => { const i = argv.indexOf(n); return i >= 0 && argv[i + 1] ? argv[i + 1] : d; };
const PORT = parseInt(getArg('--port', '8080'), 10);
const SIMULAR = argv.includes('--simular');
const ADB = getArg('--adb', 'adb');
const GAME_PORT = 7777;
const FIN_SEGUNDOS = 15;

const ESCENAS = ['Tutorial', 'Parte 1 - El Despertar', 'Parte 2 - Cumpleaños', 'Parte 3 - Pasillo de hotel',
  'Parte 4 - Pasillo de madera', 'Parte 5 - Backrooms', 'Parte Final'];

const DIR = __dirname;
const PUBLIC = path.join(DIR, 'public');
const DATOS = path.join(DIR, 'datos');
const RANKING_FILE = path.join(DATOS, 'ranking.json');
const CONFIG_FILE = path.join(DATOS, 'config.json');
fs.mkdirSync(DATOS, { recursive: true });

// ---------- persistencia ----------
function escribirAtomico(file, obj) {
  const tmp = file + '.tmp-' + process.pid;
  fs.writeFileSync(tmp, JSON.stringify(obj, null, 2), 'utf8');
  fs.renameSync(tmp, file);
}
function leerJson(file, def) {
  try { return JSON.parse(fs.readFileSync(file, 'utf8')); } catch (e) { return def; }
}
let ranking = leerJson(RANKING_FILE, { entries: [] });
if (!ranking || !Array.isArray(ranking.entries)) ranking = { entries: [] };
const config = leerJson(CONFIG_FILE, { nombre: '' });
const guardarRanking = () => escribirAtomico(RANKING_FILE, ranking);

// ---------- estado ----------
const S = {
  adb: { ok: false, msg: 'iniciando' },
  socket: { ok: false, msg: 'sin conectar' },
  simulado: SIMULAR,
  nombreProximo: config.nombre || '',
  partida: null,   // partida actual
  ultimoFin: null, // {nombre, tiempo, puesto, at}
  log: [],         // últimas líneas [CRONO]
};

function nuevaPartida(runId) {
  return {
    runId, nombre: S.nombreProximo || '', escena: '', base: 0, baseAt: Date.now(),
    congelado: false, conSalto: false, splits: [], pistas: [], iniciada: Date.now(), ultimaEscenaInicio: 0,
  };
}
function tiempoActual() {
  const p = S.partida;
  if (!p) return 0;
  return p.congelado ? p.base : p.base + (Date.now() - p.baseAt) / 1000;
}
function fijarTiempo(seg) {
  const p = S.partida; if (!p) return;
  p.base = seg; p.baseAt = Date.now();
}
function agregarSplit(seg, escena) {
  const p = S.partida; if (!p || !escena) return;
  if (p.splits.some(s => s.escena === escena)) return;
  const prev = p.splits.length ? p.splits[p.splits.length - 1].acumulado : 0;
  p.splits.push({ escena, acumulado: +seg.toFixed(1), parcial: +Math.max(0, seg - prev).toFixed(1) });
}

// ---------- ranking ----------
function ordenado() { return ranking.entries.slice().sort((a, b) => a.tiempo - b.tiempo); }
function recordsPorParte() {
  const r = {};
  for (const e of ranking.entries) for (const s of (e.parciales || [])) {
    if (!r[s.escena] || s.parcial < r[s.escena].parcial) r[s.escena] = { parcial: s.parcial, nombre: e.nombre };
  }
  return r;
}
function agregarAlRanking(p, tiempo) {
  const entry = {
    id: Date.now().toString(36) + Math.random().toString(36).slice(2, 6),
    nombre: p.nombre || 'Anónimo', tiempo: +tiempo.toFixed(1), fecha: new Date().toISOString(),
    parciales: p.splits.slice(), pistas: p.pistas.length,
  };
  ranking.entries.push(entry);
  guardarRanking();
  const puesto = ordenado().findIndex(e => e.id === entry.id) + 1;
  S.ultimoFin = { nombre: entry.nombre, tiempo: entry.tiempo, puesto, at: Date.now(), abortada: false };
}

// ---------- parser de líneas del juego ----------
function logLinea(txt) {
  S.log.push({ t: new Date().toLocaleTimeString('es-AR'), txt });
  if (S.log.length > 30) S.log.shift();
}
function num(s) { const n = parseFloat(String(s).replace(',', '.')); return isFinite(n) ? n : 0; }

function procesarLinea(raw) {
  const i = raw.indexOf('[CRONO] ');
  if (i < 0) return;
  const txt = raw.slice(i + 8).trim();
  if (!txt) return;
  logLinea(txt);
  const sp = txt.indexOf(' ');
  const cmd = sp < 0 ? txt : txt.slice(0, sp);
  const rest = sp < 0 ? '' : txt.slice(sp + 1).trim();
  const seg2 = () => { const j = rest.indexOf(' '); return j < 0 ? [num(rest), ''] : [num(rest.slice(0, j)), rest.slice(j + 1).trim()]; };
  let p = S.partida;

  switch (cmd) {
    case 'start': {
      S.partida = p = nuevaPartida(rest);
      S.ultimoFin = null;
      if (S.nombreProximo) enviarAlJuego('name ' + S.nombreProximo).catch(() => {});
      break;
    }
    case 'scene': {
      if (!p) { S.partida = p = nuevaPartida('?'); }
      const [seg, esc] = seg2();
      p.escena = esc; fijarTiempo(seg); p.congelado = false;
      break;
    }
    case 'split': { if (!p) break; const [seg, esc] = seg2(); agregarSplit(seg, esc); break; }
    case 'loadstart': { if (!p) break; fijarTiempo(num(rest)); p.congelado = true; break; }
    case 'loadend': { if (!p) break; fijarTiempo(num(rest)); p.congelado = false; break; }
    case 'hint': {
      if (!p) break;
      const m = rest.match(/^(\S+)\s+(\S+)\s*(.*)$/);
      if (m) p.pistas.push({ escalon: m[1], seg: num(m[2]), escena: m[3] });
      break;
    }
    case 'tick': {
      if (!p) break;
      const [seg, esc] = seg2();
      fijarTiempo(seg); if (esc) p.escena = esc;
      break;
    }
    case 'name': {
      if (p) p.nombre = rest;
      break;
    }
    case 'end': {
      if (!p) break;
      const seg = num(rest);
      fijarTiempo(seg); p.congelado = true;
      agregarSplit(seg, p.escena);
      if (p.conSalto) S.ultimoFin = { nombre: p.nombre || 'Jugador', tiempo: +seg.toFixed(1), puesto: 0, at: Date.now(), abortada: false, conSalto: true };
      else agregarAlRanking(p, seg);
      S.partida = null;
      break;
    }
    case 'abort': {
      if (!p) break;
      S.ultimoFin = { nombre: p.nombre, tiempo: num(rest), puesto: 0, at: Date.now(), abortada: true };
      S.partida = null;
      break;
    }
    case 'cmd': { if (p && /^skip ok(s|$)/.test(rest)) p.conSalto = true; break; }
    default: break;
  }
  emitirEstado();
}

// ---------- estado hacia clientes ----------
function estadoPublico() {
  const p = S.partida;
  return {
    serverNow: Date.now(),
    adb: S.adb, socket: S.socket, simulado: S.simulado,
    nombreProximo: S.nombreProximo,
    partida: p ? {
      nombre: p.nombre, escena: p.escena, tiempo: +tiempoActual().toFixed(2), congelado: p.congelado,
      splits: p.splits, pistas: p.pistas.length, conSalto: p.conSalto, runId: p.runId,
    } : null,
    ultimoFin: S.ultimoFin,
    finSegundos: FIN_SEGUNDOS,
    escenas: ESCENAS,
    top: ordenado().slice(0, 10).map((e, i) => ({ puesto: i + 1, id: e.id, nombre: e.nombre, tiempo: e.tiempo, pistas: e.pistas })),
    total: ranking.entries.length,
    records: recordsPorParte(),
    log: S.log,
  };
}
// Lista completa para el operador (borrar un tiempo).
function listaRanking() {
  return ordenado().map((e, i) => ({ puesto: i + 1, id: e.id, nombre: e.nombre, tiempo: e.tiempo, fecha: e.fecha }));
}
const clientes = new Set();
function emitirEstado() {
  const est = estadoPublico();
  est.lista = listaRanking();
  const data = 'event: state\ndata: ' + JSON.stringify(est) + '\n\n';
  for (const c of clientes) { try { c.write(data); } catch (e) { clientes.delete(c); } }
}
setInterval(() => {
  const p = S.partida;
  const data = 'event: hb\ndata: ' + JSON.stringify({
    serverNow: Date.now(), activa: !!p, tiempo: +tiempoActual().toFixed(2), congelado: p ? p.congelado : false,
  }) + '\n\n';
  for (const c of clientes) { try { c.write(data); } catch (e) { clientes.delete(c); } }
}, 1000);

// ---------- adb / logcat ----------
let logcatProc = null;
function bufferLineas(cb) {
  let resto = '';
  return chunk => {
    resto += chunk.toString('utf8');
    let k;
    while ((k = resto.indexOf('\n')) >= 0) { cb(resto.slice(0, k).replace(/\r$/, '')); resto = resto.slice(k + 1); }
  };
}
function setAdb(ok, msg) {
  if (S.adb.ok !== ok || S.adb.msg !== msg) { S.adb = { ok, msg }; emitirEstado(); }
}
function iniciarLogcat() {
  if (SIMULAR) return;
  execFile(ADB, ['logcat', '-c'], { timeout: 8000 }, () => {
    let proc;
    try { proc = spawn(ADB, ['logcat', '-s', 'Unity', '-v', 'time']); }
    catch (e) { setAdb(false, 'no se pudo lanzar adb: ' + e.message); return setTimeout(iniciarLogcat, 3000); }
    logcatProc = proc;
    let recibio = false;
    const t0 = Date.now();
    proc.on('error', e => {
      setAdb(false, e.code === 'ENOENT' ? 'adb no encontrado (' + ADB + ')' : 'error de adb: ' + e.message);
    });
    proc.stdout.on('data', bufferLineas(l => { if (!recibio) { recibio = true; } procesarLinea(l); }));
    proc.stderr.on('data', d => setAdb(false, d.toString().trim().split('\n')[0].slice(0, 120)));
    // si adb sigue vivo 2 s sin morir lo damos por bueno
    setTimeout(() => { if (logcatProc === proc && proc.exitCode === null) setAdb(true, 'logcat activo'); }, 2000);
    proc.on('close', code => {
      if (logcatProc === proc) logcatProc = null;
      setAdb(false, 'logcat cortado (código ' + code + '), reintentando');
      setTimeout(iniciarLogcat, 3000);
    });
    void t0;
  });
}

// ---------- socket de comandos ----------
let sock = null;
let pendientes = []; // {resolve, reject, timer}
let ultimoPong = 0;
function setSocket(ok, msg) {
  if (S.socket.ok !== ok || S.socket.msg !== msg) { S.socket = { ok, msg }; emitirEstado(); }
}
function limpiarPendientes(err) {
  for (const p of pendientes) { clearTimeout(p.timer); p.reject(new Error(err)); }
  pendientes = [];
}
function conectarSocket() {
  if (SIMULAR) return;
  if (sock) return;
  execFile(ADB, ['forward', 'tcp:' + GAME_PORT, 'tcp:' + GAME_PORT], { timeout: 6000 }, (err) => {
    if (err) {
      setSocket(false, 'adb forward falló');
      return setTimeout(conectarSocket, 3000);
    }
    const s = net.createConnection({ host: '127.0.0.1', port: GAME_PORT });
    sock = s;
    s.setEncoding('utf8');
    const fin = msg => {
      if (sock !== s) return;
      sock = null; s.destroy();
      limpiarPendientes('socket cerrado');
      setSocket(false, msg);
      setTimeout(conectarSocket, 3000);
    };
    s.on('connect', () => { ultimoPong = Date.now(); setSocket(true, 'conectado'); });
    s.on('error', e => fin('sin conexión al juego (' + (e.code || e.message) + ')'));
    s.on('close', () => fin('socket cerrado'));
    s.on('data', bufferLineas(l => {
      const p = pendientes.shift();
      if (p) { clearTimeout(p.timer); p.resolve(l); }
    }));
  });
}
// ping periódico
setInterval(() => {
  if (SIMULAR || !sock || !S.socket.ok) return;
  enviarAlJuego('ping').then(r => { if (r === 'pong') ultimoPong = Date.now(); }).catch(() => {});
  if (Date.now() - ultimoPong > 15000 && sock) { try { sock.destroy(); } catch (e) {} }
}, 5000);

function enviarAlJuego(linea) {
  if (SIMULAR) return Promise.resolve(simularComando(linea));
  return new Promise((resolve, reject) => {
    if (!sock || !S.socket.ok) return reject(new Error('sin conexión con el juego'));
    const entry = { resolve, reject, timer: null };
    entry.timer = setTimeout(() => {
      pendientes = pendientes.filter(x => x !== entry);
      reject(new Error('el juego no respondió'));
    }, 3000);
    pendientes.push(entry);
    sock.write(linea.replace(/[\r\n]+/g, ' ') + '\n');
  });
}

// ---------- simulador ----------
function simularComando(linea) {
  const [c, ...r] = linea.split(' ');
  if (c === 'ping') return 'pong';
  if (c === 'status') return S.partida ? 'status 1 ' + tiempoActual().toFixed(1) + ' ' + S.partida.escena : 'status 0 0 -';
  if (c === 'name') { simLinea('name ' + r.join(' ')); return 'ok'; }
  if (c === 'hint') { if (!S.partida) return 'err sin partida'; simLinea('hint 1 ' + tiempoActual().toFixed(1) + ' ' + S.partida.escena); simLinea('cmd hint ok'); return 'ok'; }
  if (c === 'skip') { if (!S.partida) return 'err no-transition'; simLinea('cmd skip ok'); return 'ok'; }
  if (c === 'menu') { if (S.partida) simLinea('abort ' + tiempoActual().toFixed(1)); simLinea('cmd menu ok'); return 'ok'; }
  return 'err comando desconocido';
}
const SIM_PREFIJO = '11-05 14:02:11.123  1234  1250 I Unity   : [CRONO] ';
function simLinea(t) { procesarLinea(SIM_PREFIJO + t); }
function iniciarSimulacion() {
  setAdb(true, 'simulado (sin adb)');
  setSocket(true, 'simulado');
  const t0 = () => Date.now();
  let inicio = 0, tFrozen = 0, frozenAcum = 0, congelado = false;
  const seg = () => ((congelado ? tFrozen : t0()) - inicio - frozenAcum) / 1000;
  const pasos = []; // [msDesdeInicio, fn]
  let t = 1000;
  const dur = [10, 18, 20, 18, 16, 18, 12];
  pasos.push([0, () => { inicio = t0(); simLinea('start 20261105-140211'); if (!S.nombreProximo) simLinea('name Jugador Demo'); }]);
  ESCENAS.forEach((esc, i) => {
    const s0 = t;
    pasos.push([s0, () => simLinea(`scene ${seg().toFixed(1)} ${esc}`)]);
    if (i === 2) pasos.push([s0 + 6000, () => simLinea(`hint 1 ${seg().toFixed(1)} ${esc}`)]);
    if (i === 4) pasos.push([s0 + 5000, () => simLinea(`hint 2 ${seg().toFixed(1)} ${esc}`)]);
    const fin = s0 + dur[i] * 1000;
    if (i < ESCENAS.length - 1) {
      pasos.push([fin, () => simLinea(`split ${seg().toFixed(1)} ${esc}`)]);
      pasos.push([fin + 100, () => { tFrozen = t0(); congelado = true; simLinea(`loadstart ${seg().toFixed(1)}`); }]);
      pasos.push([fin + 1600, () => { frozenAcum += t0() - tFrozen; congelado = false; simLinea(`loadend ${seg().toFixed(1)}`); }]);
      t = fin + 1700;
    } else {
      pasos.push([fin, () => simLinea(`split ${seg().toFixed(1)} ${esc}`)]);
      pasos.push([fin + 50, () => simLinea(`end ${seg().toFixed(1)}`)]);
    }
  });
  for (let ms = 5000; ms < t + 12000; ms += 5000) {
    pasos.push([ms, () => { if (S.partida && !congelado) simLinea(`tick ${seg().toFixed(1)} ${S.partida.escena}`); }]);
  }
  const base = Date.now() + 1500;
  for (const [ms, fn] of pasos) setTimeout(fn, Math.max(0, base + ms - Date.now()));
  console.log('Simulación: una partida falsa de ~' + Math.round(t / 1000) + ' s empieza en 1,5 s.');
}

// ---------- HTTP ----------
const MIME = { '.html': 'text/html; charset=utf-8', '.css': 'text/css', '.js': 'text/javascript', '.json': 'application/json',
  '.ttf': 'font/ttf', '.txt': 'text/plain; charset=utf-8', '.png': 'image/png', '.ico': 'image/x-icon' };
function enviarJson(res, code, obj) {
  res.writeHead(code, { 'Content-Type': 'application/json; charset=utf-8', 'Cache-Control': 'no-store' });
  res.end(JSON.stringify(obj));
}
function leerCuerpo(req) {
  return new Promise(resolve => {
    let b = '';
    req.on('data', d => { b += d; if (b.length > 1e5) req.destroy(); });
    req.on('end', () => { try { resolve(JSON.parse(b || '{}')); } catch (e) { resolve({}); } });
  });
}
function servirArchivo(res, rel) {
  const f = path.normalize(path.join(PUBLIC, rel));
  if (!f.startsWith(PUBLIC + path.sep) && f !== PUBLIC) { res.writeHead(403); return res.end('prohibido'); }
  fs.readFile(f, (err, data) => {
    if (err) { res.writeHead(404, { 'Content-Type': 'text/plain; charset=utf-8' }); return res.end('no encontrado'); }
    res.writeHead(200, { 'Content-Type': MIME[path.extname(f).toLowerCase()] || 'application/octet-stream', 'Cache-Control': 'no-cache' });
    res.end(data);
  });
}
const CMDS_OK = new Set(['hint', 'skip', 'menu', 'ping', 'status']);

const server = http.createServer(async (req, res) => {
  const url = req.url.split('?')[0];
  try {
    if (req.method === 'GET') {
      if (url === '/') return servirArchivo(res, 'index.html');
      if (url === '/operador') return servirArchivo(res, 'operador.html');
      if (url === '/events') {
        res.writeHead(200, { 'Content-Type': 'text/event-stream', 'Cache-Control': 'no-cache', Connection: 'keep-alive' });
        clientes.add(res);
        req.on('close', () => clientes.delete(res));
        const est = estadoPublico(); est.lista = listaRanking();
        res.write('retry: 2000\nevent: state\ndata: ' + JSON.stringify(est) + '\n\n');
        return;
      }
      if (url.startsWith('/') && !url.includes('..')) return servirArchivo(res, url.slice(1));
    }
    if (req.method === 'POST') {
      const b = await leerCuerpo(req);
      if (url === '/api/cmd') {
        const cmd = String(b.cmd || '');
        if (cmd === 'name') {
          if (!b.arg) return enviarJson(res, 400, { ok: false, error: 'falta el nombre' });
        } else if (!CMDS_OK.has(cmd)) return enviarJson(res, 400, { ok: false, error: 'comando no permitido' });
        try {
          const r = await enviarAlJuego(cmd + (b.arg ? ' ' + b.arg : ''));
          return enviarJson(res, 200, { ok: !r.startsWith('err'), respuesta: r });
        } catch (e) { return enviarJson(res, 200, { ok: false, error: e.message }); }
      }
      if (url === '/api/nombre') {
        const nombre = String(b.nombre || '').replace(/[\r\n]/g, ' ').trim().slice(0, 24);
        S.nombreProximo = nombre;
        escribirAtomico(CONFIG_FILE, { nombre });
        let resp = 'guardado';
        if (nombre) { try { resp = await enviarAlJuego('name ' + nombre); } catch (e) { resp = 'guardado (el juego no está conectado, se manda al empezar)'; } }
        emitirEstado();
        return enviarJson(res, 200, { ok: true, respuesta: resp });
      }
      if (url === '/api/ranking/borrar') {
        const antes = ranking.entries.length;
        ranking.entries = ranking.entries.filter(e => e.id !== b.id);
        if (ranking.entries.length === antes) return enviarJson(res, 404, { ok: false, error: 'no existe ese tiempo' });
        guardarRanking(); emitirEstado();
        return enviarJson(res, 200, { ok: true });
      }
      if (url === '/api/ranking/reiniciar') {
        let backup = null;
        if (ranking.entries.length) {
          const f = 'ranking-' + new Date().toISOString().replace(/[:.]/g, '-') + '.json';
          escribirAtomico(path.join(DATOS, f), ranking); backup = f;
        }
        ranking = { entries: [] }; guardarRanking(); S.ultimoFin = null; emitirEstado();
        return enviarJson(res, 200, { ok: true, backup });
      }
    }
    res.writeHead(404, { 'Content-Type': 'text/plain; charset=utf-8' }); res.end('no encontrado');
  } catch (e) {
    console.error(e); try { enviarJson(res, 500, { ok: false, error: String(e.message) }); } catch (_) {}
  }
});

server.on('error', e => { console.error('No se pudo abrir el puerto ' + PORT + ': ' + e.message); process.exit(1); });
server.listen(PORT, () => {
  console.log('Operador listo.');
  console.log('  Zona pública:  http://localhost:' + PORT + '/');
  console.log('  Operador:      http://localhost:' + PORT + '/operador');
  console.log(SIMULAR ? '  Modo simulación (sin adb).' : '  adb: ' + ADB);
  if (SIMULAR) iniciarSimulacion(); else { iniciarLogcat(); conectarSocket(); }
});
process.on('SIGINT', () => { if (logcatProc) logcatProc.kill(); process.exit(0); });
