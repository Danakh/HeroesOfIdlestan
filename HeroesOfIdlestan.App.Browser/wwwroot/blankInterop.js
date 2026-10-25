// Module d'interop du head navigateur, importé par BrowserInterop.InitializeAsync.
// Il ne couvre que ce qu'Avalonia.Browser n'expose pas : stockage, visibilité de l'onglet, Web Audio.

// ── Stockage local ──────────────────────────────────────────────────────────────
// localStorage peut lever (navigation privée Safari, quota dépassé) : une exception ici remonterait
// dans le runtime .NET et interromprait la sauvegarde automatique.

export function getStorageItem(key) {
    try { return localStorage.getItem(key); }
    catch (e) { console.error('[Blank] lecture localStorage impossible', e); return null; }
}

export function setStorageItem(key, value) {
    try { localStorage.setItem(key, value); }
    catch (e) { console.error('[Blank] écriture localStorage impossible', e); }
}

export function removeStorageItem(key) {
    try { localStorage.removeItem(key); }
    catch (e) { console.error('[Blank] suppression localStorage impossible', e); }
}

// ── Page ────────────────────────────────────────────────────────────────────────

export function appReady() {
    document.getElementById('loading')?.remove();
}

let hiddenAt = null;

export function registerVisibilityHandler(onVisible) {
    document.addEventListener('visibilitychange', () => {
        if (document.visibilityState === 'hidden') {
            hiddenAt = Date.now();
        } else if (document.visibilityState === 'visible' && hiddenAt !== null) {
            const elapsed = (Date.now() - hiddenAt) / 1000;
            hiddenAt = null;
            // Sous la demi-seconde, c'est un simple changement de focus : le rattraper ferait sauter le jeu.
            if (elapsed > 0.5) onVisible(elapsed);
        }
    });
}

export function registerHiddenHandler(onChanged) {
    document.addEventListener('visibilitychange', () => onChanged(document.visibilityState === 'hidden'));
}

// ── Bruitages (Web Audio) ───────────────────────────────────────────────────────
// Deux contraintes du navigateur :
//   - un AudioContext créé hors geste utilisateur démarre « suspended » : on le réveille au premier
//     clic ou à la première touche ;
//   - decodeAudioData est asynchrone : un son déclenché avant la fin du décodage ne joue rien.

let audioContext = null;
let audioSuspended = false;
const audioBuffers = new Map();

function ensureAudioContext() {
    if (audioContext) return audioContext;
    const Ctor = window.AudioContext || window.webkitAudioContext;
    if (!Ctor) return null;
    try {
        audioContext = new Ctor();
        resumeOnGesture();
        return audioContext;
    } catch (e) {
        console.error('[Blank] AudioContext indisponible', e);
        return null;
    }
}

function resumeOnGesture() {
    const wake = () => {
        if (!audioSuspended) audioContext?.resume?.().catch(() => {});
        if (!audioContext || audioContext.state === 'running') {
            document.removeEventListener('pointerdown', wake);
            document.removeEventListener('keydown', wake);
        }
    };
    document.addEventListener('pointerdown', wake);
    document.addEventListener('keydown', wake);
}

// `wav` est le fichier brut ; decodeAudioData consomme l'ArrayBuffer, on lui en donne une copie.
export function audioLoad(name, wav) {
    const ctx = ensureAudioContext();
    if (!ctx) return;
    const copy = new Uint8Array(wav).buffer;
    ctx.decodeAudioData(copy)
        .then((buffer) => audioBuffers.set(name, buffer))
        .catch((e) => console.error('[Blank] WAV illisible : ' + name, e));
}

export function audioPlay(name, volume) {
    const ctx = audioContext;
    if (!ctx || ctx.state !== 'running') return;
    const buffer = audioBuffers.get(name);
    if (!buffer) return;
    // Une source Web Audio est à usage unique : c'est ce qui permet à un son de se superposer à lui-même.
    const source = ctx.createBufferSource();
    source.buffer = buffer;
    const gain = ctx.createGain();
    gain.gain.value = Math.max(0, Math.min(1, volume));
    source.connect(gain).connect(ctx.destination);
    source.start();
}

export function audioSetSuspended(suspended) {
    audioSuspended = suspended;
    if (!audioContext) return;
    (suspended ? audioContext.suspend?.() : audioContext.resume?.())?.catch?.(() => {});
}

export function audioClose() {
    audioBuffers.clear();
    audioContext?.close?.().catch(() => {});
    audioContext = null;
}
