"""
Generateur des bruitages du squelette.

Les sons sont synthetises ici puis ecrits en WAV mono 16 bits 44100 Hz dans
HeroesOfIdlestan.Core.Presentation/Resources/sounds/, d'ou ils sont embarques (voir HeroesOfIdlestan.Core.Presentation.csproj).
Chaque fichier porte le nom de la valeur de SoundId qui le joue, en snake_case : c'est ce qui les
relie (voir SoundBank.FileName).

Relancer apres toute retouche :

    python assets/sounds/generate_sounds.py

Remplacer un son par un vrai enregistrement ne demande aucun code : deposer un WAV du meme nom
dans Resources/sounds/ (mono ou stereo, 16 bits ou 32 bits flottants, toute frequence) et le
retirer de SOUNDS ci-dessous pour que le script ne l'ecrase plus.

Deux regles pour tous les sons :
  - duree courte (< 1 s) : ce sont des retours d'interface, pas de la musique ;
  - fondu de fin systematique (voir `finish`), sinon la coupure nette claque.
"""

import math
import os
import struct
import wave

SAMPLE_RATE = 44100
OUT_DIR = os.path.join(os.path.dirname(os.path.abspath(__file__)),
                       "..", "..", "HeroesOfIdlestan.Core.Presentation", "Resources", "sounds")


def tone(freq, duration, volume=0.5, attack=0.005):
    """Sinus avec une attaque courte et une decroissance exponentielle."""
    n = int(duration * SAMPLE_RATE)
    out = []
    for i in range(n):
        t = i / SAMPLE_RATE
        env = min(1.0, t / attack) * math.exp(-4.0 * t / duration)
        out.append(volume * env * math.sin(2 * math.pi * freq * t))
    return out


def mix_at(base, other, offset_seconds):
    start = int(offset_seconds * SAMPLE_RATE)
    end = start + len(other)
    if end > len(base):
        base.extend([0.0] * (end - len(base)))
    for i, v in enumerate(other):
        base[start + i] += v
    return base


def finish(samples, fade=0.02):
    """Fondu de fin, puis normalisation douce sous 0.8 de crete."""
    n = int(fade * SAMPLE_RATE)
    for i in range(min(n, len(samples))):
        samples[-1 - i] *= i / n
    peak = max(1e-9, max(abs(s) for s in samples))
    if peak > 0.8:
        samples = [s * 0.8 / peak for s in samples]
    return samples


# -- Sons --------------------------------------------------------------------

def ui_confirm():
    s = tone(880, 0.09, 0.45)
    return finish(mix_at(s, tone(1320, 0.10, 0.35), 0.045))


def achievement_unlocked():
    s = []
    for i, f in enumerate([523.25, 659.25, 783.99, 1046.5]):
        mix_at(s, tone(f, 0.45, 0.35), i * 0.09)
    return finish(s, fade=0.08)


def zone_cleared():
    s = tone(660, 0.12, 0.35)
    return finish(mix_at(s, tone(990, 0.16, 0.30), 0.06))


def fight_lost():
    s = []
    for i, f in enumerate([392.0, 329.63, 261.63]):
        mix_at(s, tone(f, 0.35, 0.35), i * 0.14)
    return finish(s, fade=0.08)


def level_up():
    s = []
    for i, f in enumerate([523.25, 783.99, 1046.5]):
        mix_at(s, tone(f, 0.25, 0.30), i * 0.06)
    return finish(s, fade=0.05)


SOUNDS = {
    "ui_confirm": ui_confirm,
    "achievement_unlocked": achievement_unlocked,
    "zone_cleared": zone_cleared,
    "fight_lost": fight_lost,
    "level_up": level_up,
}


def write_wav(name, samples):
    path = os.path.join(OUT_DIR, name + ".wav")
    with wave.open(path, "wb") as w:
        w.setnchannels(1)
        w.setsampwidth(2)
        w.setframerate(SAMPLE_RATE)
        w.writeframes(b"".join(struct.pack("<h", int(max(-1.0, min(1.0, s)) * 32767)) for s in samples))
    print(f"{name}.wav  {len(samples) / SAMPLE_RATE:.2f} s")


if __name__ == "__main__":
    os.makedirs(OUT_DIR, exist_ok=True)
    for name, build in SOUNDS.items():
        write_wav(name, build())
