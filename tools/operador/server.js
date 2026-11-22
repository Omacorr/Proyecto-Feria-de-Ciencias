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
const PKG = 'com.omarcorrea.proyectointegrador';
const SIN_SENAL_MS = 12000;   // sin líneas [CRONO] durante este tiempo => se congela la partida
const PID_CADA_MS = 5000;

const ESCENAS = ['Tutorial', 'Parte 1 - El Despertar', 'Parte 2 - Cumpleaños', 'Parte 3 - Pasillo de hotel',
  'Parte 4 - Pasillo de madera', 'Parte 5 - Backrooms', 'Parte Final'];

const ESCENAS_GOTO = ESCENAS.concat(['Menu Principal']);

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
  ultimaLinea: Date.now(), // hora de la última línea [CRONO] recibida (cualquiera)
  pid: null,       // true/false según adb shell pidof; null = no se pudo saber
  perf: null,      // {fps, min, peorMs, escena, at}
  perfTabla: {},   // escena -> {n, sumFps, min, peorMs}
  opciones: null,  // último status parseado
};

function nuevaPartida(runId) {
  return {
    runId, nombre: S.nombreProximo || '', escena: '', base: 0, baseAt: Date.now(),
    congelado: false, desc: false, conSalto: false, splits: [], pistas: [], iniciada: Date.now(), ultimaEscenaInicio: 0,
  };
}
function tiempoActual() {
  const p = S.partida;
  if (!p) return 0;
  return (p.congelado || p.pausado) ? p.base : p.base + (Date.now() - p.baseAt) / 1000;
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
  S.ultimaLinea = Date.now();
  if (S.partida && S.partida.desc) {   // volvió a hablar: descongelar
    S.partida.desc = false; S.partida.congelado = false; S.partida.baseAt = Date.now();
  }
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
      if (!p.pausado) fijarTiempo(seg); if (esc) p.escena = esc;
      break;
    }
    case 'pause': { if (!p) break; fijarTiempo(num(rest)); p.pausado = true; break; }   // el operador paró el tiempo
    case 'resume': { if (!p) break; fijarTiempo(num(rest)); p.pausado = false; break; }
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
    case 'perf': {
      const m = rest.match(/^(\S+)\s+(\S+)\s+(\S+)\s+(?:cpu=(\S+)\s+gpu=(\S+)\s+)?(.*)$/);
      if (!m) break;
      registrarPerf(num(m[1]), num(m[2]), num(m[3]), m[6].trim(), m[4] ? num(m[4]) : null, m[5] ? num(m[5]) : null);
      break;
    }
    case 'cmd': { if (p && /^(skip|goto) ok(\s|$)/.test(rest)) p.conSalto = true; break; }
    default: break;
  }
  emitirEstado();
}

function registrarPerf(fps, min, peorMs, escena, cpuMs, gpuMs) {
  S.perf = { fps, min, peorMs, escena, cpuMs, gpuMs, at: Date.now() };
  const k = escena || '?';
  const t = S.perfTabla[k] || (S.perfTabla[k] = { n: 0, sumFps: 0, min: Infinity, peorMs: 0, nT: 0, sumCpu: 0, sumGpu: 0 });
  t.n++; t.sumFps += fps; t.min = Math.min(t.min, min); t.peorMs = Math.max(t.peorMs, peorMs);
  if (gpuMs != null) { t.nT++; t.sumCpu += cpuMs; t.sumGpu += gpuMs; }
}
function tablaPerf() {
  return Object.keys(S.perfTabla).map(k => {
    const t = S.perfTabla[k];
    return { escena: k, n: t.n, fps: +(t.sumFps / t.n).toFixed(1), min: +t.min.toFixed(1), peorMs: +t.peorMs.toFixed(1),
      cpu: t.nT ? +(t.sumCpu / t.nT).toFixed(1) : null, gpu: t.nT ? +(t.sumGpu / t.nT).toFixed(1) : null };
  });
}
function parseStatus(l) {
  const m = l.match(/^status (\d) (\S+) fps=(\S+) target=(\d+) fair=(\d) portals=(\S+)(?: paused=(\d))?(?: gfx=(\S+))?\s*(.*)$/);
  if (!m) return null;
  // "obj=<objetivo_con_guiones_bajos> hint=<n>" puede venir antes o después de la escena: se busca en cualquier lugar.
  const resto = m[9];
  const obj = resto.match(/(?:^|\s)obj=(\S+)/);
  const hint = resto.match(/(?:^|\s)hint=(\d+)/);
  const escena = resto.replace(/(?:^|\s)(?:obj|hint)=\S+/g, ' ').trim();
  return { activa: m[1] === '1', tiempo: num(m[2]), fps: num(m[3]), target: parseInt(m[4], 10),
    fair: m[5] === '1', portals: m[6], pausado: m[7] === '1', gfx: parseGfx(m[8]), escena, at: Date.now(),
    objetivo: obj ? obj[1].replace(/_/g, ' ') : null, pista: hint ? parseInt(hint[1], 10) : null };
}
// "bloom:low,post:1,hdr:1,scale:0.8" -> { bloom: "low", post: "on", hdr: "on", scale: "0.8" }
function parseGfx(t) {
  if (!t) return null;
  const o = {};
  t.split(",").forEach(kv => { const [k, v] = kv.split(":"); o[k] = (k === "post" || k === "hdr") ? (v === "1" ? "on" : "off") : v; });
  return o;
}
// Sin líneas [CRONO] por SIN_SENAL_MS: se congela la partida en el último tiempo conocido.
function congelarPorSilencio() {
  const p = S.partida;
  if (!p || p.desc || p.congelado) return;
  const silencio = Date.now() - S.ultimaLinea;
  if (silencio < SIN_SENAL_MS) return;
  p.base = Math.max(0, tiempoActual() - silencio / 1000); p.baseAt = Date.now();
  p.congelado = true; p.desc = true;
  emitirEstado();
}
function abortarPartida(motivo) {
  const p = S.partida;
  if (!p) return;
  S.ultimoFin = { nombre: p.nombre, tiempo: +tiempoActual().toFixed(1), puesto: 0, at: Date.now(), abortada: true, motivo };
  S.partida = null;
  emitirEstado();
}
function estadoJuego() {
  const p = S.partida;
  if (p && p.desc) return { vivo: false, msg: 'juego desconectado' };
  if (S.pid === false) return { vivo: false, msg: 'juego cerrado' };
  if (S.pid === true) return { vivo: true, msg: '' };
  return { vivo: null, msg: 'sin datos del proceso' };
}
function revisarPid() {
  const fin = pid => {
    const cambio = S.pid !== pid;
    S.pid = pid;
    if (pid === false && S.partida) abortarPartida('juego cerrado'); else if (cambio) emitirEstado();
  };
  if (SIMULAR) return fin(sim.vivo);
  execFile(ADB, ['shell', 'pidof', PKG], { timeout: 4000 }, (err, stdout, stderr) => {
    const out = String(stdout || '').trim();
    if (/^\d+/.test(out)) return fin(true);
    // pidof sin proceso: salida vacía, exit 1, sin mensaje de error de adb
    if (err && !err.killed && err.code === 1 && !String(stderr || '').trim()) return fin(false);
    fin(null); // adb falló (sin dispositivo, timeout): no sabemos, no abortamos
  });
}
setInterval(congelarPorSilencio, 1000);
setInterval(revisarPid, PID_CADA_MS);
function consultarStatus() {
  if (!SIMULAR && !S.socket.ok) return Promise.resolve();
  return enviarAlJuego('status').then(r => {
    const o = parseStatus(r);
    if (o) { S.opciones = o; emitirEstado(); }
  }).catch(() => {});
}
setInterval(consultarStatus, 5000);

// ---------- estado hacia clientes ----------
function estadoPublico() {
  const p = S.partida;
  return {
    serverNow: Date.now(),
    adb: S.adb, socket: S.socket, simulado: S.simulado,
    nombreProximo: S.nombreProximo,
    partida: p ? {
      nombre: p.nombre, escena: p.escena, tiempo: +tiempoActual().toFixed(2), congelado: p.congelado || !!p.pausado, pausado: !!p.pausado, desconectado: !!p.desc,
      splits: p.splits, pistas: p.pistas.length, conSalto: p.conSalto, runId: p.runId,
    } : null,
    ultimoFin: S.ultimoFin,
    finSegundos: FIN_SEGUNDOS,
    escenas: ESCENAS,
    escenasGoto: ESCENAS_GOTO,
    juego: estadoJuego(),
    perf: S.perf, perfTabla: tablaPerf(), opciones: S.opciones,
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
    serverNow: Date.now(), activa: !!p, tiempo: +tiempoActual().toFixed(2), congelado: p ? (p.congelado || !!p.pausado) : false,
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
  if (SIMULAR) { try { return Promise.resolve(simularComando(linea)); } catch (e) { return Promise.reject(e); } }
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
// Máquina de estados que imita al juego: partida, cargas, perf cada 5 s y comandos.
const FPS_ESCENA = { 'Tutorial': 59, 'Parte 1 - El Despertar': 57, 'Parte 2 - Cumpleaños': 31, 'Parte 3 - Pasillo de hotel': 52,
  'Parte 4 - Pasillo de madera': 46, 'Parte 5 - Backrooms': 58, 'Parte Final': 60, 'Menu Principal': 60 };
const DUR_ESCENA = [10, 18, 20, 18, 16, 18, 12]; // segundos de cada escena (simulado)
const sim = { vivo: true, colgado: false, run: null, target: 60, fair: 1, portals: 'visible', escena: 'Menu Principal', n: 0 };
const SIM_PREFIJO = '11-05 14:02:11.123  1234  1250 I Unity   : [CRONO] ';
function simLinea(t) { if (sim.vivo && !sim.colgado) procesarLinea(SIM_PREFIJO + t); }
const simSeg = () => sim.run ? sim.run.t : 0;
function simEscena(esc) {
  sim.escena = esc;
  if (sim.run) { sim.run.escenaT = 0; simLinea(`scene ${simSeg().toFixed(1)} ${esc}`); }
}
function simCarga(ms, despues) {
  const r = sim.run; if (!r) return;
  simLinea(`loadstart ${r.t.toFixed(1)}`); r.cargando = true;
  r.carga = setTimeout(() => { r.cargando = false; simLinea(`loadend ${r.t.toFixed(1)}`); despues && despues(); }, ms);
}
function simIniciar() {
  if (sim.run) clearTimeout(sim.run.carga);
  sim.run = { t: 0, i: 0, escenaT: 0, cargando: false, carga: null, hint1: false, hint2: false };
  simLinea('start 20261105-140211');
  if (!S.nombreProximo) simLinea('name Jugador Demo');
  simEscena(ESCENAS[0]);
}
function simAbrir() { sim.vivo = true; sim.colgado = false; if (!S.partida) setTimeout(simIniciar, 1500); }
function simCerrar() { sim.vivo = false; if (sim.run) clearTimeout(sim.run.carga); sim.run = null; }
function simTick100() {
  const r = sim.run;
  if (!sim.vivo || sim.colgado || !r || r.cargando) return;
  r.t += 0.1; r.escenaT += 0.1;
  const esc = ESCENAS[r.i];
  if (!r.hint1 && r.i === 2 && r.escenaT >= 6) { r.hint1 = true; simLinea(`hint 1 ${r.t.toFixed(1)} ${esc}`); }
  if (!r.hint2 && r.i === 4 && r.escenaT >= 5) { r.hint2 = true; simLinea(`hint 2 ${r.t.toFixed(1)} ${esc}`); }
  if (sim.escena === esc && r.escenaT >= DUR_ESCENA[r.i]) {
    simLinea(`split ${r.t.toFixed(1)} ${esc}`);
    if (r.i < ESCENAS.length - 1) { r.i++; simCarga(1600, () => simEscena(ESCENAS[r.i])); }
    else { simLinea(`end ${r.t.toFixed(1)}`); sim.run = null; }
  }
}
function simPerf() {
  if (!sim.vivo || sim.colgado) return;
  const base = Math.min(sim.target, FPS_ESCENA[sim.escena] || 55);
  const fps = Math.max(5, base + (Math.random() - 0.5) * 6);
  const min = Math.max(3, fps - 4 - Math.random() * 14);
  const ms = 1000 / min * (1.2 + Math.random());
  if (sim.run && !sim.run.cargando) simLinea(`tick ${simSeg().toFixed(1)} ${sim.escena}`);
  simLinea(`perf ${fps.toFixed(1)} ${min.toFixed(1)} ${ms.toFixed(1)} ${sim.escena}`);
}
function simularComando(linea) {
  const [c, ...r] = linea.split(' ');
  const arg = r.join(' ');
  if (!sim.vivo) throw new Error('sin conexión con el juego');
  if (c === 'ping') return 'pong';
  if (c === 'status') {
    const t = sim.run ? sim.run.t.toFixed(1) : '0.0';
    return `status ${sim.run ? 1 : 0} ${t} fps=${(Math.min(sim.target, FPS_ESCENA[sim.escena] || 55) - 1.3).toFixed(1)} target=${sim.target} fair=${sim.fair} portals=${sim.portals} paused=${sim.pausado ? 1 : 0} gfx=${simGfx()} ${sim.escena} obj=Candado hint=1`;
  }
  if (c === 'name') { simLinea('name ' + arg); return 'ok'; }
  if (c === 'pause' || c === 'resume') { if (!sim.run) return 'err no-run'; simLinea(c + ' ' + simSeg().toFixed(1)); simLinea('cmd ' + c + ' ok'); return 'ok'; }
  if (c === 'hint') { if (!sim.run) return 'err sin partida'; simLinea(`hint 1 ${simSeg().toFixed(1)} ${sim.escena}`); simLinea('cmd hint ok'); return 'ok'; }
  if (c === 'skip') { if (!sim.run) return 'err no-transition'; sim.run.escenaT = 999; simLinea('cmd skip ok'); return 'ok'; }
  if (c === 'menu') { if (sim.run) { simLinea('abort ' + simSeg().toFixed(1)); clearTimeout(sim.run.carga); sim.run = null; } sim.escena = 'Menu Principal'; simLinea('cmd menu ok'); return 'ok'; }
  if (c === 'goto') {
    if (!ESCENAS_GOTO.includes(arg)) return 'err escena desconocida';
    if (arg === 'Menu Principal') { if (sim.run) { simLinea('abort ' + simSeg().toFixed(1)); clearTimeout(sim.run.carga); sim.run = null; } sim.escena = arg; simLinea('cmd goto ok ' + arg); return 'ok'; }
    if (!sim.run) return 'err sin partida';
    const k = ESCENAS.indexOf(arg);
    clearTimeout(sim.run.carga); sim.run.cargando = false;
    sim.run.i = k; simLinea('cmd goto ok ' + arg);
    simCarga(1200, () => simEscena(arg));
    return 'ok';
  }
  if (c === 'restart') {
    if (!sim.run) return 'err sin partida';
    sim.run.escenaT = 0; simLinea('cmd restart ok');
    simCarga(1200, () => simEscena(ESCENAS[sim.run.i]));
    return 'ok';
  }
  if (c === 'fair') { sim.fair = arg === 'on' ? 1 : 0; return 'ok'; }
  if (c === 'portals') { sim.portals = arg; return 'ok'; }
  if (c === 'fps') { sim.target = parseInt(arg, 10); return 'ok'; }
  if (['bloom', 'post', 'hdr', 'scale'].includes(c)) { sim.gfx = sim.gfx || { bloom: 'low', post: 'on', hdr: 'on', scale: '0.8' }; sim.gfx[c] = arg; return 'ok'; }
  // Comandos nuevos del visor (sustos, sonidos, tensión, pistas, texto): en simulación solo se registran.
  if (['scare', 'tension', 'sfx', 'hintlvl', 'say'].includes(c)) { simLinea('cmd ' + c + ' ok'); return 'ok'; }
  return 'err comando desconocido';
}
function simGfx() {
  const g = sim.gfx || { bloom: 'low', post: 'on', hdr: 'on', scale: '0.8' };
  return `bloom:${g.bloom},post:${g.post === 'on' ? 1 : 0},hdr:${g.hdr === 'on' ? 1 : 0},scale:${g.scale}`;
}
function iniciarSimulacion() {
  setAdb(true, 'simulado (sin adb)');
  setSocket(true, 'simulado');
  setInterval(simTick100, 100);
  setInterval(simPerf, 5000);
  setTimeout(() => { simIniciar(); }, 1500);
  S.pid = true;
  console.log('Simulación: una partida falsa de ~2 min empieza en 1,5 s. POST /api/sim/{colgar,reanudar,cerrar,abrir} para simular fallos.');
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
// Sustos y sonidos que acepta el juego (ver ExamineScareSounds / Screamer / WhisperGenerator en el proyecto).
const SUSTOS = ['screamer', 'whisper', 'steps', 'distant', 'knock', 'blackout', 'heartbeat'];
const SFX = ['susurro_01', 'susurro_02', 'susurro_03', 'susurro_04', 'susurro_05', 'respiracion_01', 'pasos_concreto', 'pasos_madera',
  'golpes_madera', 'crujido_madera_01', 'crujido_madera_02', 'crujido_madera_03', 'golpe_seco', 'monstruo_rugido', 'monstruo_respiracion',
  'latidos_despertar', 'tv_estatica', 'grito_screamer', 'derrumbe_madera', 'caida_viento', 'zumbido_grave'];
const CMDS_OK = new Set(['hint', 'skip', 'menu', 'ping', 'status', 'goto', 'restart', 'fair', 'portals', 'fps', 'pause', 'resume', 'bloom', 'post', 'hdr', 'scale', 'fx',
  'scare', 'tension', 'sfx', 'hintlvl', 'say']);
const ARGS_OK = { fair: ['on', 'off'], portals: ['visible', 'subtle'], fps: ['30', '60'], bloom: ['off', 'low', 'full'], post: ['on', 'off'], hdr: ['on', 'off'], scale: ['0.5', '0.6', '0.7', '0.8', '1'],
  hintlvl: ['1', '2', '3'] };
// "say": quita saltos de línea y tabs, colapsa espacios y recorta. Se aplica antes de validar y antes de reenviar.
function limpiarTexto(t) { return String(t).replace(/\s+/g, ' ').trim(); }
function validarTexto(t) {
  if (!t) return 'el texto está vacío';
  if (Array.from(t).length > 120) return 'máximo 120 caracteres';
  if (!/^[\p{L}\p{N}\p{P}\p{S} ]+$/u.test(t)) return 'hay caracteres no imprimibles';
  return null;
}
function validarTension(a) {
  if (!/^\d*\.?\d+$/.test(a)) return 'tensión no válida (número entre 0 y 1)';
  const n = Number(a);
  return n >= 0 && n <= 1 ? null : 'tensión entre 0 y 1';
}
// devuelve null si el comando es válido, o el motivo del rechazo
function validarCmd(cmd, arg) {
  if (!CMDS_OK.has(cmd)) return 'comando no permitido';
  if (cmd === 'goto') return ESCENAS_GOTO.includes(arg) ? null : 'escena no válida';
  if (cmd === 'scare') return SUSTOS.includes(arg) ? null : 'tipo de susto no válido';
  if (cmd === 'sfx') return SFX.includes(arg) ? null : 'sonido no válido';
  if (cmd === 'tension') return validarTension(arg);
  if (cmd === 'say') return validarTexto(arg);
  // fx: diagnóstico del post-procesado (PostFxDebug.cs), no se guarda en el juego.
  if (cmd === 'fx') return /^(dither|vignette|tonemap|color|grain|nrp) (on|off)$|^inter (auto|always)$|^bfilter (gaussian|dual|kawase)$/.test(arg) ? null : 'argumento no válido';
  if (ARGS_OK[cmd]) return ARGS_OK[cmd].includes(arg) ? null : 'argumento no válido (' + ARGS_OK[cmd].join('/') + ')';
  return null;
}
function runAdb(args, timeout) {
  return new Promise(resolve => execFile(ADB, args, { timeout }, (err, stdout, stderr) =>
    resolve({ err, out: (String(stdout || '') + String(stderr || '')).trim() })));
}

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
        let arg = b.arg == null ? '' : String(b.arg);
        if (cmd === 'say') arg = limpiarTexto(arg);
        if (cmd === 'name') {
          if (!arg) return enviarJson(res, 400, { ok: false, error: 'falta el nombre' });
        } else {
          const motivo = validarCmd(cmd, arg);
          if (motivo) return enviarJson(res, 400, { ok: false, error: motivo });
        }
        try {
          const r = await enviarAlJuego(cmd + (arg ? ' ' + arg : ''));
          if (['fair', 'portals', 'fps', 'goto', 'restart', 'bloom', 'post', 'hdr', 'scale'].includes(cmd)) setTimeout(consultarStatus, 600);
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
      if (url === '/api/perf/borrar') {
        S.perfTabla = {}; emitirEstado();
        return enviarJson(res, 200, { ok: true });
      }
      if (url === '/api/app/abrir') {
        if (SIMULAR) { simAbrir(); return enviarJson(res, 200, { ok: true, respuesta: 'simulado' }); }
        const r = await runAdb(['shell', 'monkey', '-p', PKG, '-c', 'android.intent.category.LAUNCHER', '1'], 10000);
        const ok = !r.err && !/error|aborted/i.test(r.out);
        return enviarJson(res, 200, ok ? { ok: true, respuesta: 'abriendo el juego' } : { ok: false, error: r.out.slice(0, 160) || 'adb falló' });
      }
      if (url === '/api/app/cerrar') {
        let ok = true, error = '';
        if (SIMULAR) simCerrar();
        else { const r = await runAdb(['shell', 'am', 'force-stop', PKG], 8000); if (r.err) { ok = false; error = r.out.slice(0, 160) || 'adb falló'; } }
        if (ok) { S.pid = false; abortarPartida('juego cerrado'); emitirEstado(); }
        return enviarJson(res, 200, ok ? { ok: true, respuesta: 'juego cerrado' } : { ok: false, error });
      }
      if (SIMULAR && url.startsWith('/api/sim/')) {
        const a = url.slice(9);
        if (a === 'colgar') sim.colgado = true;
        else if (a === 'reanudar') sim.colgado = false;
        else if (a === 'cerrar') simCerrar();
        else if (a === 'abrir') simAbrir();
        else return enviarJson(res, 404, { ok: false });
        return enviarJson(res, 200, { ok: true });
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
